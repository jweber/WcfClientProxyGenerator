using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using JetBrains.Annotations;

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

        private readonly IDictionary<Type, Predicate<Exception>> _exceptionsToHandle;

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
            _exceptionsToHandle = new Dictionary<Type, Predicate<Exception>>
            {
                { typeof(ChannelTerminatedException), null },
                { typeof(EndpointNotFoundException), null },
                { typeof(ServerTooBusyException), null }
            };
        }

        public void AddExceptionToRetryOn<TException>(Predicate<Exception> where = null)
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

        [UsedImplicitly]
        public TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method)
        {
            var provider = RefreshProvider(null);

            try
            {
                Exception mostRecentException = null;
                for (int i = 0; i < RetryCount; i++)
                {
                    try
                    {
                        return method(provider);
                    }
                    catch (Exception ex)
                    {
                        var exceptionType = ex.GetType();

                        if (_exceptionsToHandle.ContainsKey(exceptionType)
                            && (_exceptionsToHandle[exceptionType] == null
                                || _exceptionsToHandle[exceptionType](ex)))
                        {
                            mostRecentException = ex;
                            Thread.Sleep(MillisecondsBetweenRetries * (i + 1));
                            provider = RefreshProvider(provider);
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

            return default(TResponse);
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
