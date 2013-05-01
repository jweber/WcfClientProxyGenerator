using System;
using System.Collections.Generic;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Linq;
using JetBrains.Annotations;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    internal class RetryingWcfActionInvoker<TServiceInterface> : IActionInvoker<TServiceInterface> 
        where TServiceInterface : class
    {
        /// <summary>
        /// Number of times the client will attempt to retry
        /// calls to the service in the event of some known WCF
        /// exceptions occurring
        /// </summary>
        public int RetryCount { get; set; }

        public int MillisecondsBetweenRetries { get; set; }

        private readonly IDictionary<Type, object> _exceptionsToHandle;
        private readonly IDictionary<Type, object> _responsesToRetryOn;

        /// <summary>
        /// The method that initializes new WCF action providers
        /// </summary>
        private readonly Func<TServiceInterface> _wcfActionProviderCreator;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wcfActionProviderCreator"></param>
        /// <param name="retryCount"></param>
        /// <param name="millisecondsBetweenRetries">Performs a linear backoff between retries by multiplying this value by an incremntal value up to <paramref name="retryCount"/></param>
        public RetryingWcfActionInvoker(
            Func<TServiceInterface> wcfActionProviderCreator, 
            int retryCount = 5, 
            int millisecondsBetweenRetries = 1000)
        {
            RetryCount = retryCount;
            MillisecondsBetweenRetries = millisecondsBetweenRetries;

            _wcfActionProviderCreator = wcfActionProviderCreator;
            _exceptionsToHandle = new Dictionary<Type, object>
            {
                { typeof(ChannelTerminatedException), null },
                { typeof(EndpointNotFoundException), null },
                { typeof(ServerTooBusyException), null }
            };

            _responsesToRetryOn = new Dictionary<Type, object>();
        }

        public void AddExceptionToRetryOn<TException>(Predicate<TException> where = null)
            where TException : Exception
        {
            if (where == null)
            {
                where = _ => true;
            }

            _exceptionsToHandle.Add(typeof(TException), where);
        }

        public void AddExceptionToRetryOn(Type exceptionType, Predicate<Exception> where = null)
        {
            if (where == null)
            {
                where = _ => true;
            }

            _exceptionsToHandle.Add(exceptionType, where);
        }

        public void AddResponseToRetryOn<TResponse>(Predicate<TResponse> where)
        {
            _responsesToRetryOn.Add(typeof(TResponse), where);
        }

        [UsedImplicitly]
        public void Invoke(Action<TServiceInterface> method)
        {
            Invoke(provider =>
            {
                method(provider);
                return true;
            });
        }

        [UsedImplicitly]
        public TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method)
        {
            var provider = RefreshProvider(null);

            TResponse lastResponse = default(TResponse);

            try
            {
                Exception mostRecentException = null;
                for (int i = 0; i < RetryCount; i++)
                {
                    try
                    {
                        TResponse response = method(provider);
                        if (ResponseInRetryable(response))
                        {
                            lastResponse = response;
                            Delay(i, ref provider);
                            continue;
                        }
                        
                        return response;
                    }
                    catch (Exception ex)
                    {
                        if (ExceptionIsRetryable(ex))
                        {
                            mostRecentException = ex;
                            Delay(i, ref provider);
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

        private void Delay(int iteration, ref TServiceInterface provider)
        {
            Thread.Sleep(MillisecondsBetweenRetries * (iteration + 1));
            provider = RefreshProvider(provider);
        }

        private bool ExceptionIsRetryable(Exception ex)
        {
            return EvaluatePredicate(ex.GetType(), ex, _exceptionsToHandle);
        }

        private bool ResponseInRetryable<TResponse>(TResponse response)
        {
            Type @type = typeof(TResponse);
            var baseTypes = @type.GetAllInheritedTypes();

            return baseTypes.Any(t => EvaluatePredicate(t, response, _responsesToRetryOn));
        }

        private bool EvaluatePredicate<TInstance>(Type key, TInstance instance, IDictionary<Type, object> dictionary)
        {
            if (!dictionary.ContainsKey(key))
                return false;

            object predicate = dictionary[key];

            if (predicate == null)
                return true;

            Type predicateType = typeof(Predicate<>).MakeGenericType(key);

            var invokeMethod = predicateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            bool response = (bool) invokeMethod.Invoke(predicate, new object[] { instance });

            return response;
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
