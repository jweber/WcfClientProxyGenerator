using System;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using WcfClientProxyGenerator.Async;
using WcfClientProxyGenerator.Policy;
using WcfClientProxyGenerator.Util;

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

        /// <summary>
        /// Allows inspecting and modifying the <typeparamref name="TResponse"/> object
        /// before returning the response to the calling method.
        /// </summary>
        /// <typeparam name="TResponse">Type or parent type/interface of the response</typeparam>
        /// <param name="where">Predicate to filter responses based on its parameters</param>
        /// <param name="handler">
        /// Delegate that takes a <typeparamref name="TResponse"/>
        /// </param>
        public void HandleResponse<TResponse>(Predicate<TResponse> @where, Action<TResponse> handler)
        {
            _actionInvoker.AddResponseHandler(r =>
            {
                handler(r);
                return r;
            }, @where);
        }

        /// <summary>
        /// Allows inspecting and modifying the <typeparamref name="TResponse"/> object
        /// before returning the response to the calling method.
        /// </summary>
        /// <typeparam name="TResponse">Type or parent type/interface of the response</typeparam>
        /// <param name="handler">
        /// Delegate that takes a <typeparamref name="TResponse"/>
        /// </param>    
        public void HandleResponse<TResponse>(Action<TResponse> handler)
        {
            _actionInvoker.AddResponseHandler<TResponse>(r =>
            {
                handler(r);
                return r;
            }, null);
        }

        /// <summary>
        /// Allows inspecting and modifying the <typeparamref name="TResponse"/> object
        /// before returning the response to the calling method.
        /// </summary>
        /// <typeparam name="TResponse">Type or parent type/interface of the response</typeparam>
        /// <param name="handler">
        /// Delegate that takes a <typeparamref name="TResponse"/> and returns a <typeparamref name="TResponse"/>
        /// </param>        
        public void HandleResponse<TResponse>(Func<TResponse, TResponse> handler)
        {
            _actionInvoker.AddResponseHandler(handler, null);
        }

        /// <summary>
        /// Allows inspecting and modifying the <typeparamref name="TResponse"/> object
        /// before returning the response to the calling method.
        /// </summary>
        /// <typeparam name="TResponse">Type or parent type/interface of the response</typeparam>
        /// <param name="where">Predicate to filter responses based on its parameters</param>
        /// <param name="handler">
        /// Delegate that takes a <typeparamref name="TResponse"/> and returns a <typeparamref name="TResponse"/>
        /// </param>
        public void HandleResponse<TResponse>(Predicate<TResponse> @where, Func<TResponse, TResponse> handler)
        {
            _actionInvoker.AddResponseHandler(handler, @where);
        }

        #region IRetryingProxyConfigurator

        /// <summary>
        /// Event that is fired immediately before the service method will be called. This event
        /// is called only once per request.
        /// </summary>
        public event OnCallBeginHandler OnCallBegin
        {
            add { _actionInvoker.OnCallBegin += value; }
            remove { _actionInvoker.OnCallBegin -= value; }
        }

        /// <summary>
        /// Event that is fired immediately after the request successfully or unsuccessfully completes.
        /// </summary>
        public event OnCallSuccessHandler OnCallSuccess
        {
            add { _actionInvoker.OnCallSuccess += value; }
            remove { _actionInvoker.OnCallSuccess -= value; }
        }

        /// <summary>
        /// Fires before the invocation of a service method, at every retry.
        /// </summary>
        public event OnInvokeHandler OnBeforeInvoke
        {
            add { _actionInvoker.OnBeforeInvoke += value; }
            remove { _actionInvoker.OnBeforeInvoke -= value; }
        }

        /// <summary>
        /// Fires after the successful invocation of a method.
        /// </summary>
        public event OnInvokeHandler OnAfterInvoke
        {
            add { _actionInvoker.OnAfterInvoke += value; }
            remove { _actionInvoker.OnAfterInvoke -= value; }
        }

        /// <summary>
        /// Fires after the successful invocation of a method.
        /// </summary>
        public event OnExceptionHandler OnException
        {
            add { _actionInvoker.OnException += value; }
            remove { _actionInvoker.OnException -= value; }
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
            // If TServiceInterface is our generated async interface, the ChannelFactory
            // will look for a config based on the *Async contract. We need to fix this manually.
            if (typeof(TServiceInterface).GetCustomAttribute<GeneratedAsyncInterfaceAttribute>() != null)
            {
                Type originalServiceInterfaceType = typeof(TServiceInterface).GetInterfaces()[0];
                _channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(originalServiceInterfaceType);
            }
            else
            {
                _channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(typeof(TServiceInterface));    
            }
        }

        public void SetEndpoint(string endpointConfigurationName)
        {
            if (typeof(TServiceInterface).HasAttribute<GeneratedAsyncInterfaceAttribute>())
            {
                Type originalServiceInterfaceType = typeof(TServiceInterface).GetInterfaces()[0];
                _channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(endpointConfigurationName, originalServiceInterfaceType);
            }
            else
            {
                _channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(endpointConfigurationName);    
            }
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

        public void RetryFailureExceptionFactory(RetryFailureExceptionFactoryDelegate factory)
        {
            _actionInvoker.RetryFailureExceptionFactory = factory;
        }

        #endregion
    }
}