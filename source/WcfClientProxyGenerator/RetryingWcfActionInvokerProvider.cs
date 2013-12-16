using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using WcfClientProxyGenerator.Policy;

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
            _actionInvoker = new RetryingWcfActionInvoker<TServiceInterface>(() =>
            {
                if (_channelFactory == null)
                    this.UseDefaultEndpoint();

                return _channelFactory.CreateChannel();
            });
        }

        public IActionInvoker<TServiceInterface> ActionInvoker
        {
            get { return _actionInvoker; }
        }

        #region IRetryingProxyConfigurator
        /// <summary>
        /// Fires before the invocation of a service method, at every retry.
        /// </summary>
        public event OnInvokeHandler OnBeforeInvoke
        {
            add
            {
                _actionInvoker.OnBeforeInvoke += value;
            }

            remove
            {
                _actionInvoker.OnBeforeInvoke -= value;
            }
        }

        /// <summary>
        /// Fires after the successful invocation of a method.
        /// </summary>
        public event OnInvokeHandler OnAfterInvoke
        {
            add
            {
                _actionInvoker.OnAfterInvoke += value;
            }

            remove
            {
                _actionInvoker.OnAfterInvoke -= value;
            }
        }

        /// <summary>
        /// Fires after the successful invocation of a method.
        /// </summary>
        public event OnExceptionHandler OnException
        {
            add
            {
                _actionInvoker.OnException += value;
            }

            remove
            {
                _actionInvoker.OnException -= value;
            }
        }

        /// <summary>
        /// Allows access to WCF extensibility features.
        /// </summary>
        public ChannelFactory ChannelFactory
        {
            get
            {
                // if requested without endpoint set, use default
                if (_channelFactory == null)
                {
                    UseDefaultEndpoint();
                }

                return _channelFactory;
            }
        }

        public void UseDefaultEndpoint()
        {
            _channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>();
        }

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

        public void SetDelayPolicy(Func<IDelayPolicy> policyFactory)
        {
            _actionInvoker.DelayPolicyFactory = policyFactory;
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