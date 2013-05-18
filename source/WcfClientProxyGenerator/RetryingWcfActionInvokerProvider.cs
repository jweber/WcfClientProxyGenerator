using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using JetBrains.Annotations;

namespace WcfClientProxyGenerator
{
    internal class RetryingWcfActionInvokerProvider<TServiceInterface> : 
        IActionInvokerProvider<TServiceInterface>, 
        IRetryingProxyConfigurator
        where TServiceInterface : class
    {
        private ChannelFactory<TServiceInterface> _channelFactory;
        private readonly RetryingWcfActionInvoker<TServiceInterface> _actionInvoker; 

        public RetryingWcfActionInvokerProvider()
        {
            _actionInvoker = new RetryingWcfActionInvoker<TServiceInterface>(() => _channelFactory.CreateChannel());
        }

        [UsedImplicitly]
        public IActionInvoker<TServiceInterface> ActionInvoker
        {
            get { return _actionInvoker; }
        }

        #region IRetryingProxyConfigurator

        public void SetEndpoint(string endpointConfigurationName)
        {
            _channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(endpointConfigurationName);
        }

        public void SetEndpoint(Binding binding, EndpointAddress endpointAddress)
        {
            _channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(binding, endpointAddress);
        }

        public void MaximumRetries(int retryCount)
        {
            _actionInvoker.RetryCount = retryCount;
        }

        public void TimeBetweenRetries(TimeSpan timeSpan)
        {
            _actionInvoker.MillisecondsBetweenRetries = timeSpan.Milliseconds;
        }

        public void RetryOnException<TException>(Predicate<TException> where = null)
            where TException : Exception
        {
            _actionInvoker.AddExceptionToRetryOn<TException>(where);
        }

        public void RetryOnException(Type exceptionType, Predicate<Exception> where = null)
        {
            _actionInvoker.AddExceptionToRetryOn(exceptionType, where);
        }

        public void RetryOnResponse<TResponse>(Predicate<TResponse> where)
        {
            _actionInvoker.AddResponseToRetryOn(where);
        }

        #endregion
    }
}