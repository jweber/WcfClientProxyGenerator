using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;

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
        internal const int WcfRetries = 5;

        private IDictionary<Type, Predicate<Exception>> exceptionsToHandle;

        /// <summary>
        /// The method that initializes new WCF action providers
        /// </summary>
        private readonly Func<TServiceInterface> wcfActionProviderCreator;

        public RetryingWcfActionInvoker(Func<TServiceInterface> wcfActionProviderCreator)
        {
            this.wcfActionProviderCreator = wcfActionProviderCreator;
            this.exceptionsToHandle = new Dictionary<Type, Predicate<Exception>>
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

            this.exceptionsToHandle.Add(typeof(TException), where);
        }

        public void AddExceptionToRetryOn(Type exceptionType, Predicate<Exception> where = null)
        {
            if (where == null)
            {
                where = _ => true;
            }

            this.exceptionsToHandle.Add(exceptionType, where);
        }

        public TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method)
        {
            var provider = this.RefreshProvider(null);

            try
            {
                Exception mostRecentException = null;
                for (int i = 0; i < WcfRetries; i++)
                {
                    try
                    {
                        return method(provider);
                    }
                    catch (Exception ex)
                    {
                        var exceptionType = ex.GetType();

                        if (this.exceptionsToHandle.ContainsKey(exceptionType)
                            && (this.exceptionsToHandle[exceptionType] == null
                                || this.exceptionsToHandle[exceptionType](ex)))
                        {
                            mostRecentException = ex;
                            Thread.Sleep(1000 * (i + 1));
                            provider = this.RefreshProvider(provider);
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
                        string.Format("WCF call failed after {0} retries.", WcfRetries),
                        mostRecentException);
                }
            }
            finally
            {
                this.DisposeProvider(provider);
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
                return this.wcfActionProviderCreator();
            }

            if (communicationObject.State == CommunicationState.Opened)
            {
                return provider;
            }

            this.DisposeProvider(provider);
            return this.wcfActionProviderCreator();
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
