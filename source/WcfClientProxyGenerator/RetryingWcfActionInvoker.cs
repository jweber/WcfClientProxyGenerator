using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Security.Principal;
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

        private static ConcurrentDictionary<Type, Lazy<MethodInfo>> ResponseHandlerPredicateCache
            = new ConcurrentDictionary<Type, Lazy<MethodInfo>>();
        
        private static ConcurrentDictionary<Type, Lazy<MethodInfo>> ResponseHandlerCache
            = new ConcurrentDictionary<Type, Lazy<MethodInfo>>();

        private readonly Type _originalServiceInterfaceType;

        private readonly IDictionary<Type, IList<object>> _retryPredicates;
        private readonly IDictionary<Type, IList<PredicateHandlerHolder>> _responseHandlers;
        
        /// <summary>
        /// The method that initializes new WCF action providers
        /// </summary>
        private readonly Func<TServiceInterface> _wcfActionProviderCreator;

        public RetryingWcfActionInvoker(
            Func<TServiceInterface> wcfActionProviderCreator, 
            Func<IDelayPolicy> delayPolicyFactory = null,
            int retryCount = 0)
        {
            RetryCount = retryCount;
            DelayPolicyFactory = delayPolicyFactory ?? DefaultProxyConfigurator.DefaultDelayPolicyFactory;
            RetryFailureExceptionFactory = DefaultProxyConfigurator.DefaultRetryFailureExceptionFactory;

            _wcfActionProviderCreator = wcfActionProviderCreator;
            _retryPredicates = new Dictionary<Type, IList<object>>
            {
                { typeof(ChannelTerminatedException), new List<object> { null } },
                { typeof(EndpointNotFoundException), new List<object> { null } },
                { typeof(ServerTooBusyException), new List<object> { null } }
            };

            _responseHandlers = new Dictionary<Type, IList<PredicateHandlerHolder>>();

            _originalServiceInterfaceType = GetOriginalServiceInterface();
        }

        /// <summary>
        /// Number of times the client will attempt to retry
        /// calls to the service in the event of some known WCF
        /// exceptions occurring
        /// </summary>
        public int RetryCount { get; set; }

        public Func<IDelayPolicy> DelayPolicyFactory { get; set; }

        public RetryFailureExceptionFactoryDelegate RetryFailureExceptionFactory { get; set; }

        /// <summary>
        /// Event that is fired immediately before the service method will be called. This event
        /// is called only once per request.
        /// </summary>
        public event OnCallBeginHandler OnCallBegin = delegate { };

        /// <summary>
        /// Event that is fired immediately after the request successfully or unsuccessfully completes.
        /// </summary>
        public event OnCallSuccessHandler OnCallSuccess = delegate { };

        /// <summary>
        /// Fires before the invocation of a service method, at every retry.
        /// </summary>
        public event OnInvokeHandler OnBeforeInvoke = delegate { };

        /// <summary>
        /// Fires after the successful invocation of a method.
        /// </summary>
        public event OnInvokeHandler OnAfterInvoke = delegate { };  

        /// <summary>
        /// Fires when an exception happens during the invocation of a service method, at every retry.
        /// </summary>
        public event OnExceptionHandler OnException = delegate { }; 

        public void AddExceptionToRetryOn<TException>(Predicate<TException> where = null)
            where TException : Exception
        {
            if (where == null)
            {
                where = _ => true;
            }

            if (!_retryPredicates.ContainsKey(typeof(TException)))
                _retryPredicates.Add(typeof(TException), new List<object>());

            _retryPredicates[typeof(TException)].Add(where);
        }

        public void AddExceptionToRetryOn(Type exceptionType, Predicate<Exception> where = null)
        {
            if (where == null)
            {
                where = _ => true;
            }

            if (!_retryPredicates.ContainsKey(exceptionType))
                _retryPredicates.Add(exceptionType, new List<object>());

            _retryPredicates[exceptionType].Add(where);
        }

        public void AddResponseToRetryOn<TResponse>(Predicate<TResponse> where)
        {
            if (!_retryPredicates.ContainsKey(typeof(TResponse)))
                _retryPredicates.Add(typeof(TResponse), new List<object>());

            _retryPredicates[typeof(TResponse)].Add(where);
        }

        public void AddResponseHandler<TResponse>(Func<TResponse, TResponse> handler, Predicate<TResponse> @where)
        {
            if (!_responseHandlers.ContainsKey(typeof(TResponse)))
                _responseHandlers.Add(typeof(TResponse), new List<PredicateHandlerHolder>());

            _responseHandlers[typeof(TResponse)].Add(new PredicateHandlerHolder
            {
                Predicate = @where, 
                Handler = handler
            });
        }

        /// <summary>
        /// Used to identify void return types in the Invoke() methods below.
        /// </summary>
        private struct VoidReturnType { }

        private Type GetOriginalServiceInterface()
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
        /// <param name="invokeInfo"></param>
        public void Invoke(Action<TServiceInterface> method, InvokeInfo invokeInfo = null)
        {
            Invoke(provider =>
            {
                method(provider);
                return new VoidReturnType();
            }, invokeInfo);
        }

        /// <summary>
        /// This function is called when a proxy's method is called that should return something.
        /// </summary>
        /// <param name="method">Method implementing the service call using WCF</param>
        /// <param name="invokeInfo"></param>
        public TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method, InvokeInfo invokeInfo = null)
        {
            TServiceInterface provider = this.RefreshProvider(null, 0, invokeInfo);
            TResponse lastResponse = default(TResponse);
            IDelayPolicy delayPolicy = this.DelayPolicyFactory();

            var sw = Stopwatch.StartNew();

            try
            {
                this.HandleOnCallBegin(invokeInfo);

                Exception mostRecentException = null;
                for (int i = 0; i < this.RetryCount + 1; i++)
                {
                    try
                    {
                        this.HandleOnBeforeInvoke(i, invokeInfo);

                        // make the service call
                        TResponse response = method(provider);

                        this.HandleOnAfterInvoke(i, response, invokeInfo);

                        if (this.ResponseInRetryable(response))
                        {
                            lastResponse = response;
                            provider = this.Delay(i, delayPolicy, provider, invokeInfo);
                            continue;
                        }

                        sw.Stop();

                        response = this.ExecuteResponseHandlers(response);

                        this.HandleOnCallSuccess(sw.Elapsed, response, (i + 1), invokeInfo);
    
                        return response;
                    }
                    catch (Exception ex)
                    {
                        this.HandleOnException(ex, i, invokeInfo);

                        if (this.ExceptionIsRetryable(ex))
                        {
                            mostRecentException = ex;
                            provider = this.Delay(i, delayPolicy, provider, invokeInfo);
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

                    var exception = this.RetryFailureExceptionFactory(this.RetryCount, mostRecentException, invokeInfo);
                    throw exception;
                }
            }
            finally
            {
                this.DisposeProvider(provider, -1, invokeInfo);
            }

            return lastResponse;
        }

        public Task InvokeAsync(Func<TServiceInterface, Task> method, InvokeInfo invokeInfo = null)
        {
            return this.InvokeAsync(async provider =>
            {
                await method(provider).ConfigureAwait(false);
                return Task.FromResult(true);
            }, invokeInfo);
        }

        public async Task<TResponse> InvokeAsync<TResponse>(Func<TServiceInterface, Task<TResponse>> method, InvokeInfo invokeInfo = null)
        {
            TServiceInterface provider = RefreshProvider(null, 0, invokeInfo);
            TResponse lastResponse = default(TResponse);
            IDelayPolicy delayPolicy = DelayPolicyFactory();

            var sw = Stopwatch.StartNew();

            try
            {
                this.HandleOnCallBegin(invokeInfo);

                Exception mostRecentException = null;
                for (int i = 0; i < RetryCount + 1; i++)
                {
                    try
                    {
                        this.HandleOnBeforeInvoke(i, invokeInfo);

                        TResponse response = await method(provider).ConfigureAwait(false);

                        this.HandleOnAfterInvoke(i, response, invokeInfo);

                        if (ResponseInRetryable(response))
                        {
                            lastResponse = response;
                            provider = await DelayAsync(i, delayPolicy, provider, invokeInfo).ConfigureAwait(false);
                            continue;
                        }

                        sw.Stop();

                        response = this.ExecuteResponseHandlers(response);

                        this.HandleOnCallSuccess(sw.Elapsed, response, (i + 1), invokeInfo);

                        return response;
                    }
                    catch (Exception ex)
                    {
                        this.HandleOnException(ex, i, invokeInfo);

                        // determine whether to retry the service call
                        if (ExceptionIsRetryable(ex))
                        {
                            mostRecentException = ex;

                            provider = await DelayAsync(i, delayPolicy, provider, invokeInfo).ConfigureAwait(false);
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

                    var exception = this.RetryFailureExceptionFactory(this.RetryCount, mostRecentException, invokeInfo);
                    throw exception;
                }
            }
            finally
            {
                DisposeProvider(provider, -1, invokeInfo);
            }
            
            return lastResponse;
        }

        private void HandleOnCallBegin(InvokeInfo invokeInfo)
        {
            this.OnCallBegin(this, new OnCallBeginHandlerArguments
            {
                ServiceType = _originalServiceInterfaceType,
                InvokeInfo = invokeInfo
            });
        }

        private void HandleOnBeforeInvoke(int retryCounter, InvokeInfo invokeInfo)
        {
            this.OnBeforeInvoke(this, new OnInvokeHandlerArguments
            {
                ServiceType = _originalServiceInterfaceType,
                RetryCounter = retryCounter,
                InvokeInfo = invokeInfo
            });
        }

        private void HandleOnAfterInvoke(int retryCounter, object response, InvokeInfo invokeInfo)
        {
            // set return value if non-void
            if (invokeInfo != null && response != null && response.GetType() != typeof(VoidReturnType))
            {
                invokeInfo.MethodHasReturnValue = true;
                invokeInfo.ReturnValue = response;
            }

            this.OnAfterInvoke(this, new OnInvokeHandlerArguments
            {
                ServiceType = _originalServiceInterfaceType,
                RetryCounter = retryCounter,
                InvokeInfo = invokeInfo,
            });            
        }

        private void HandleOnCallSuccess(TimeSpan callDuration, object response, int requestAttempts, InvokeInfo invokeInfo)
        {
            if (invokeInfo != null && response != null && response.GetType() != typeof (VoidReturnType))
            {
                invokeInfo.MethodHasReturnValue = true;
                invokeInfo.ReturnValue = response;
            }

            this.OnCallSuccess(this, new OnCallSuccessHandlerArguments
            {
                ServiceType = _originalServiceInterfaceType,
                InvokeInfo = invokeInfo,
                CallDuration = callDuration,
                RequestAttempts = requestAttempts
            });
        }

        private void HandleOnException(Exception exception, int retryCounter, InvokeInfo invokeInfo)
        {
            this.OnException(this, new OnExceptionHandlerArguments
            {
                Exception = exception,
                ServiceType = _originalServiceInterfaceType,
                RetryCounter = retryCounter,
                InvokeInfo = invokeInfo,
            });            
        }

        private TServiceInterface Delay(
            int iteration, 
            IDelayPolicy delayPolicy, 
            TServiceInterface provider, 
            InvokeInfo invokeInfo)
        {
            Thread.Sleep(delayPolicy.GetDelay(iteration));
            return RefreshProvider(provider, iteration, invokeInfo);
        }

        private async Task<TServiceInterface> DelayAsync(
            int iteration, 
            IDelayPolicy delayPolicy, 
            TServiceInterface provider, 
            InvokeInfo invokeInfo)
        {
            await Task.Delay(delayPolicy.GetDelay(iteration)).ConfigureAwait(false);
            return RefreshProvider(provider, iteration, invokeInfo);
        }

        private bool ExceptionIsRetryable(Exception ex)
        {
            return EvaluatePredicate(ex.GetType(), ex);
        }

        private TResponse ExecuteResponseHandlers<TResponse>(TResponse response)
        {
            Type @type = typeof(TResponse);
            var baseTypes = TypeHierarchyCache.GetOrAddSafe(@type, _ =>
            {
                return @type.GetAllInheritedTypes();
            });

            foreach (var baseType in baseTypes)
                response = this.ExecuteResponseHandlers(response, baseType);

            return response;
        }

        private TResponse ExecuteResponseHandlers<TResponse>(TResponse response, Type type)
        {
            if (!this._responseHandlers.ContainsKey(@type))
                return response;

            IList<PredicateHandlerHolder> responseHandlerHolders = this._responseHandlers[@type];

            MethodInfo predicateInvokeMethod = ResponseHandlerPredicateCache.GetOrAddSafe(@type, _ =>
            {
                Type predicateType = typeof(Predicate<>).MakeGenericType(@type);
                return predicateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            });

            var handlers = responseHandlerHolders
                .Where(m => m.Predicate == null
                            || ((bool) predicateInvokeMethod.Invoke(m.Predicate, new object[] { response })))
                .ToList();

            if (!handlers.Any())
                return response;

            MethodInfo handlerMethod = ResponseHandlerCache.GetOrAddSafe(@type, _ =>
            {
                Type actionType = typeof(Func<,>).MakeGenericType(@type, @type);
                return actionType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            });

            foreach (var handler in handlers)
            {
                try
                {
                    response = (TResponse) handlerMethod.Invoke(handler.Handler, new object[] { response });
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }
            }

            return response;
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

            var predicates = _retryPredicates[key];

            MethodInfo invokeMethod = PredicateCache.GetOrAddSafe(key, _ =>
            {
                Type predicateType = typeof(Predicate<>).MakeGenericType(key);
                return predicateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            });

            // See if any non-null predicate matched the instance
            bool isSuccess = predicates
                .Where(p => p != null)
                .Any(predicate => (bool) invokeMethod.Invoke(predicate, new object[] { instance }));

            // If there's a null predicate (always match), then return true
            if (!isSuccess && predicates.Any(m => m == null))
                return true;

            // Return result of running instance through predicates
            return isSuccess;
        }

        /// <summary>
        /// Refreshes the proxy by disposing and recreating it if it's faulted.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="retryCount">Number of retries to perform</param>
        /// <param name="invokeInfo"></param>
        /// <returns></returns>
        private TServiceInterface RefreshProvider(TServiceInterface provider, int retryCount, InvokeInfo invokeInfo)
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

            DisposeProvider(provider, retryCount, invokeInfo);
            provider = null;

            return _wcfActionProviderCreator();
        }

        private void DisposeProvider(TServiceInterface provider, int retryCount, InvokeInfo invokeInfo)
        {
            var communicationObject = provider as ICommunicationObject;
            if (communicationObject == null || communicationObject.State == CommunicationState.Closed)
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
            catch (CommunicationException ex)
            {
                this.HandleOnException(ex, retryCount, invokeInfo);
            }
            catch (TimeoutException ex)
            {
                this.HandleOnException(ex, retryCount, invokeInfo);
            }
            catch (Exception ex)
            {
                this.HandleOnException(ex, retryCount, invokeInfo);
                throw;
            }
            finally
            {
                if (!success)
                {
                    this.AbortServiceChannel(communicationObject, retryCount, invokeInfo);
                }
            }
        }

        private void AbortServiceChannel(ICommunicationObject communicationObject, int retryCount, InvokeInfo invokeInfo)
        {
            try
            {
                communicationObject.Abort();
            }
            catch (Exception ex)
            {
                this.HandleOnException(ex, retryCount, invokeInfo);
            }
        }
    }
}
