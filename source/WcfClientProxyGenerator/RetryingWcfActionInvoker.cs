using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using WcfClientProxyGenerator.Async;
using WcfClientProxyGenerator.Policy;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    internal class RetryingWcfActionInvoker<TServiceInterface> : IActionInvoker<TServiceInterface> 
        where TServiceInterface : class
    {
        private static ConcurrentDictionary<Type, Lazy<MethodInfo>> PredicateCache
            = new ConcurrentDictionary<Type, Lazy<MethodInfo>>();
    
        private static ConcurrentDictionary<Type, Lazy<IEnumerable<Type>>> TypeHierarchyCache
            = new ConcurrentDictionary<Type, Lazy<IEnumerable<Type>>>();

        /// <summary>
        /// Number of times the client will attempt to retry
        /// calls to the service in the event of some known WCF
        /// exceptions occurring
        /// </summary>
        public int RetryCount { get; set; }

        public Func<IDelayPolicy> DelayPolicyFactory { get; set; }

        private readonly IDictionary<Type, object> _retryPredicates;

        /// <summary>
        /// Event that is fired immediately before the service method will be called. This event
        /// is called only once per request.
        /// </summary>
        public event OnCallBeginHandler OnCallBegin;

        /// <summary>
        /// Event that is fired immediately after the request successfully or unsuccessfully completes.
        /// </summary>
        public event OnCallEndHandler OnCallEnd;

        /// <summary>
        /// Fires before the invocation of a service method, at every retry.
        /// </summary>
        public OnInvokeHandler OnBeforeInvoke { get; set; }

        /// <summary>
        /// Fires after the successful invocation of a method.
        /// </summary>
        public OnInvokeHandler OnAfterInvoke { get; set; }

        /// <summary>
        /// Fires when an exception happens during the invocation of a service method, at every retry.
        /// </summary>
        public OnExceptionHandler OnException { get; set; }

        /// <summary>
        /// The method that initializes new WCF action providers
        /// </summary>
        private readonly Func<TServiceInterface> _wcfActionProviderCreator;

        public RetryingWcfActionInvoker(
            Func<TServiceInterface> wcfActionProviderCreator, 
            Func<IDelayPolicy> delayPolicyFactory = null,
            int retryCount = 4)
        {
            RetryCount = retryCount;
            DelayPolicyFactory = delayPolicyFactory ?? DefaultProxyConfigurator.DefaultDelayPolicyFactory;

            _wcfActionProviderCreator = wcfActionProviderCreator;
            _retryPredicates = new Dictionary<Type, object>
            {
                { typeof(ChannelTerminatedException), null },
                { typeof(EndpointNotFoundException), null },
                { typeof(ServerTooBusyException), null }
            };
        }

        public void AddExceptionToRetryOn<TException>(Predicate<TException> where = null)
            where TException : Exception
        {
            if (where == null)
            {
                where = _ => true;
            }

            _retryPredicates.Add(typeof(TException), where);
        }

        public void AddExceptionToRetryOn(Type exceptionType, Predicate<Exception> where = null)
        {
            if (where == null)
            {
                where = _ => true;
            }

            _retryPredicates.Add(exceptionType, where);
        }

        public void AddResponseToRetryOn<TResponse>(Predicate<TResponse> where)
        {
            _retryPredicates.Add(typeof(TResponse), where);
        }

        /// <summary>
        /// Used to identify void return types in the Invoke() methods below.
        /// </summary>
        private struct VoidReturnType { }

        private Type GetOriginalServiceInterface<TServiceInterface>()
        {
            Type serviceType = typeof(TServiceInterface);
            if (serviceType.HasAttribute<GeneratedAsyncInterfaceAttribute>())
                serviceType = serviceType.GetInterfaces()[0];

            return serviceType;
        }

        /// <summary>
        /// This function is called when a proxy's method is called that should return void.
        /// </summary>
        /// <param name="method">Method implementing the service call using WCF</param>
        public void Invoke(Action<TServiceInterface> method, InvokeInfo invokeInfo = null)
        {
            Invoke(provider =>
            {
                method(provider);
                return new VoidReturnType();
            }, invokeInfo);
        }

        public Task InvokeAsync(Func<TServiceInterface, Task> method, InvokeInfo invokeInfo = null)
        {
            return InvokeAsync(async provider =>
            {
                await method(provider);
                return Task.FromResult(true);
            }, invokeInfo);
        }

        public async Task<TResponse> InvokeAsync<TResponse>(Func<TServiceInterface, Task<TResponse>> method, InvokeInfo invokeInfo = null)
        {
            Type serviceType = GetOriginalServiceInterface<TServiceInterface>();

            TServiceInterface provider = RefreshProvider(null);
            TResponse lastResponse = default(TResponse);
            IDelayPolicy delayPolicy = DelayPolicyFactory();

            var sw = Stopwatch.StartNew();

            try
            {
                if (OnCallBegin != null)
                {
                    OnCallBegin(this, new OnCallBeginHandlerArguments
                    {
                        ServiceType = serviceType,
                        InvokeInfo = invokeInfo
                    });
                }

                Exception mostRecentException = null;
                for (int i = 0; i < RetryCount; i++)
                {
                    try
                    {
                        // fire OnBeforeInvoke callback at every retry
                        if (OnBeforeInvoke != null)
                        {
                            OnBeforeInvoke(this, new OnInvokeHandlerArguments
                            {
                                ServiceType = serviceType,
                                RetryCounter = i,
                                InvokeInfo = invokeInfo,
                            });
                        }

                        TResponse response = await method(provider);

                        // fire OnAfterInvoke callback at successful retry
                        if (OnAfterInvoke != null)
                        {
                            // set return value if non-void
                            if (typeof(TResponse) != typeof(VoidReturnType))
                            {
                                invokeInfo.MethodHasReturnValue = true;
                                invokeInfo.ReturnValue = response;
                            }

                            OnAfterInvoke(this, new OnInvokeHandlerArguments
                            {
                                ServiceType = serviceType,
                                RetryCounter = i,
                                InvokeInfo = invokeInfo,
                            });
                        }

                        if (ResponseInRetryable(response))
                        {
                            lastResponse = response;
                            provider = await DelayAsync(i, delayPolicy, provider);
                            continue;
                        }

                        sw.Stop();

                        if (OnCallEnd != null)
                        {
                            if (typeof (TResponse) != typeof (VoidReturnType))
                            {
                                invokeInfo.MethodHasReturnValue = true;
                                invokeInfo.ReturnValue = response;
                            }

                            OnCallEnd(this, new OnCallEndHandlerArguments
                            {
                                ServiceType = serviceType,
                                InvokeInfo = invokeInfo,
                                CallDuration = sw.Elapsed
                            });
                        }

                        return response;
                    }
                    catch (Exception ex)
                    {
                        // fire OnException event when exception happened
                        if (OnException != null)
                        {
                            OnException(this, new OnExceptionHandlerArguments()
                            {
                                Exception = ex,
                                ServiceType = serviceType,
                                RetryCounter = i,
                                InvokeInfo = invokeInfo,
                            });
                        }

                        // determine whether to retry the service call
                        if (ExceptionIsRetryable(ex))
                        {
                            mostRecentException = ex;
#if CSHARP60
                            provider = await DelayAsync(i, delayPolicy, provider);
#else
                            provider = Delay(i, delayPolicy, provider);
#endif
                        }
                        else
                        {
                            throw;
                        }                    
                    }
                }

                if (mostRecentException != null)
                {
                    throw new WcfRetryFailedException(
                        string.Format("WCF call failed after {0} retries.", RetryCount),
                        mostRecentException);
                }
            }
            finally
            {
                DisposeProvider(provider);
            }
            
            return lastResponse;
        }

        /// <summary>
        /// This function is called when a proxy's method is called that should return something.
        /// </summary>
        /// <param name="method">Method implementing the service call using WCF</param>
        /// <param name="invokeInfo"></param>
        public TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method, InvokeInfo invokeInfo = null)
        {
            Type serviceType = GetOriginalServiceInterface<TServiceInterface>();

            TServiceInterface provider = RefreshProvider(null);
            TResponse lastResponse = default(TResponse);
            IDelayPolicy delayPolicy = DelayPolicyFactory();

            var sw = Stopwatch.StartNew();

            try
            {
                if (OnCallBegin != null)
                {
                    OnCallBegin(this, new OnCallBeginHandlerArguments
                    {
                        ServiceType = serviceType,
                        InvokeInfo = invokeInfo
                    });
                }

                Exception mostRecentException = null;
                for (int i = 0; i < RetryCount + 1; i++)
                {
                    try
                    {
                        // fire OnBeforeInvoke callback at every retry
                        if (OnBeforeInvoke != null)
                        {
                            OnBeforeInvoke(this, new OnInvokeHandlerArguments()
                            {
                                ServiceType = serviceType,
                                RetryCounter = i,
                                InvokeInfo = invokeInfo,
                            });
                        }

                        // make the service call
                        TResponse response = method(provider);

                        // fire OnAfterInvoke callback at successful retry
                        if (OnAfterInvoke != null)
                        {
                            // set return value if non-void
                            if (typeof(TResponse) != typeof(VoidReturnType))
                            {
                                invokeInfo.MethodHasReturnValue = true;
                                invokeInfo.ReturnValue = response;
                            }

                            OnAfterInvoke(this, new OnInvokeHandlerArguments()
                            {
                                ServiceType = serviceType,
                                RetryCounter = i,
                                InvokeInfo = invokeInfo,
                            });
                        }

                        if (ResponseInRetryable(response))
                        {
                            lastResponse = response;
                            provider = Delay(i, delayPolicy, provider);
                            continue;
                        }

                        sw.Stop();

                        if (OnCallEnd != null)
                        {
                            if (typeof (TResponse) != typeof (VoidReturnType))
                            {
                                invokeInfo.MethodHasReturnValue = true;
                                invokeInfo.ReturnValue = response;
                            }

                            OnCallEnd(this, new OnCallEndHandlerArguments
                            {
                                ServiceType = serviceType,
                                InvokeInfo = invokeInfo,
                                CallDuration = sw.Elapsed
                            });
                        }

                        return response;
                    }
                    catch (Exception ex)
                    {
                        // fire OnException event when exception happened
                        if (OnException != null)
                        {
                            OnException(this, new OnExceptionHandlerArguments()
                            {
                                Exception = ex,
                                ServiceType = serviceType,
                                RetryCounter = i,
                                InvokeInfo = invokeInfo,
                            });
                        }
                        
                        // determine whether to retry the service call
                        if (ExceptionIsRetryable(ex))
                        {
                            mostRecentException = ex;
                            provider = Delay(i, delayPolicy, provider);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                if (mostRecentException != null)
                {
                    if (RetryCount == 0)
                        throw mostRecentException;

                    throw new WcfRetryFailedException(
                        string.Format("WCF call failed after {0} attempts.", RetryCount),
                        mostRecentException);
                }
            }
            finally
            {
                DisposeProvider(provider);
            }

            return lastResponse;
        }

        private TServiceInterface Delay(int iteration, IDelayPolicy delayPolicy, TServiceInterface provider)
        {
            Thread.Sleep(delayPolicy.GetDelay(iteration));
            return RefreshProvider(provider);
        }

        private async Task<TServiceInterface> DelayAsync(int iteration, IDelayPolicy delayPolicy, TServiceInterface provider)
        {
            await Task.Delay(delayPolicy.GetDelay(iteration));
            return RefreshProvider(provider);
        }

        private bool ExceptionIsRetryable(Exception ex)
        {
            return EvaluatePredicate(ex.GetType(), ex);
        }

        private bool ResponseInRetryable<TResponse>(TResponse response)
        {
            Type @type = typeof(TResponse);
            var baseTypes = TypeHierarchyCache.GetOrAddSafe(@type, _ =>
            {
                return @type.GetAllInheritedTypes();
            });

            return baseTypes.Any(t => EvaluatePredicate(t, response));
        }

        private bool EvaluatePredicate<TInstance>(Type key, TInstance instance)
        {
            if (!_retryPredicates.ContainsKey(key))
                return false;

            object predicate = _retryPredicates[key];

            if (predicate == null)
                return true;

            MethodInfo invokeMethod = PredicateCache.GetOrAddSafe(key, _ =>
            {
                Type predicateType = typeof(Predicate<>).MakeGenericType(key);
                return predicateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            });

            return (bool) invokeMethod.Invoke(predicate, new object[] { instance });
        }

        /// <summary>
        /// Refreshes the proxy by disposing and recreating it if it's faulted.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <returns></returns>
        private TServiceInterface RefreshProvider(TServiceInterface provider)
        {
            var communicationObject = provider as ICommunicationObject;
            if (communicationObject == null)
            {
                return _wcfActionProviderCreator();
            }

            if (communicationObject.State == CommunicationState.Opened)
            {
                return provider;
            }

            DisposeProvider(provider);
            return _wcfActionProviderCreator();
        }

        private void DisposeProvider(TServiceInterface provider)
        {
            var communicationObject = provider as ICommunicationObject;
            if (communicationObject == null)
            {
                return;
            }

            bool success = false;

            try
            {
                if (communicationObject.State != CommunicationState.Faulted)
                {
                    communicationObject.Close();
                    success = true;
                }
            }
            finally
            {
                if (!success)
                {
                    communicationObject.Abort();
                }
            }
        }
    }
}
