using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using JetBrains.Annotations;

namespace WcfClientProxyGenerator
{
    internal class ActionInvokerProvider<TServiceInterface> : IProxyConfigurator
        where TServiceInterface : class
    {
        private readonly ChannelFactory<TServiceInterface> _channelFactory;
        private readonly RetryingWcfActionInvoker<TServiceInterface> _actionInvoker; 

        private ActionInvokerProvider()
        {
            _actionInvoker = new RetryingWcfActionInvoker<TServiceInterface>(() => _channelFactory.CreateChannel());
        }

        protected ActionInvokerProvider(string endpointConfigurationName)
            : this()
        {
            _channelFactory = new ChannelFactory<TServiceInterface>(endpointConfigurationName);
        }

        protected ActionInvokerProvider(Binding binding, EndpointAddress endpointAddress)
            : this()
        {
            _channelFactory = new ChannelFactory<TServiceInterface>(binding, endpointAddress);
        }

        [UsedImplicitly]
        protected RetryingWcfActionInvoker<TServiceInterface> ActionInvoker
        {
            get
            {
                return _actionInvoker;
            }
        }

        #region IProxyConfigurator

        public void MaximumRetries(int retryCount)
        {
            _actionInvoker.RetryCount = retryCount;
        }

        public void TimeBetweenRetries(TimeSpan timeSpan)
        {
            _actionInvoker.MillisecondsBetweenRetries = timeSpan.Milliseconds;
        }

        public void AddExceptionToRetryOn<TException>(Predicate<Exception> @where = null)
            where TException : Exception
        {
            _actionInvoker.AddExceptionToRetryOn<TException>(@where);
        }

        public void AddExceptionToRetryOn(Type exceptionType, Predicate<Exception> @where = null)
        {
            _actionInvoker.AddExceptionToRetryOn(exceptionType, @where);
        }

        #endregion
    }
}