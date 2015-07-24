using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using WcfClientProxyGenerator.Async;
using WcfClientProxyGenerator.Policy;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    class PredicateHandlerHolder
    {
        public object Predicate { get; set; }
        public object Handler { get; set; }
    }

    internal class RetryingWcfActionInvokerProvider<TServiceInterface> : 
        IActionInvokerProvider<TServiceInterface>, 
        IRetryingProxyConfigurator
        where TServiceInterface : class
    {
        private static ConcurrentDictionary<Type, Lazy<IEnumerable<Type>>> TypeHierarchyCache
            = new ConcurrentDictionary<Type, Lazy<IEnumerable<Type>>>();

        private static ConcurrentDictionary<Type, Lazy<MethodInfo>> RequestParameterHandlerPredicateCache
            = new ConcurrentDictionary<Type, Lazy<MethodInfo>>();

        private static ConcurrentDictionary<Type, Lazy<MethodInfo>> RequestParameterHandlerCache
            = new ConcurrentDictionary<Type, Lazy<MethodInfo>>();

        private ChannelFactory<TServiceInterface> channelFactory;
        private readonly RetryingWcfActionInvoker<TServiceInterface> actionInvoker; 

        private readonly IDictionary<Type, IList<PredicateHandlerHolder>> requestArgumentHandlers 
            = new Dictionary<Type, IList<PredicateHandlerHolder>>(); 

        public RetryingWcfActionInvokerProvider()
        {
            actionInvoker = new RetryingWcfActionInvoker<TServiceInterface>(() =>
            {
                if (channelFactory == null)
                    this.UseDefaultEndpoint();

                return channelFactory.CreateChannel();
            });
        }

        public IActionInvoker<TServiceInterface> ActionInvoker
        {
            get { return actionInvoker; }
        }

        #region HandleRequestArgument

        /// <summary>
        /// Allows inspection or modification of request arguments immediately before sending the request.
        /// </summary>
        /// <typeparam name="TArgument">Type or parent type/interface of the argument</typeparam>
        /// <param name="where">Predicate to filter the request arguments by properties of the request, or the parameter name</param>
        /// <param name="handler">Delegate that takes a <typeparamref name="TArgument"/></param>
        public void HandleRequestArgument<TArgument>(Func<TArgument, string, bool> where, Action<TArgument> handler)
        {
            this.HandleRequestArgument(where, r =>
            {
                handler(r);
                return r;
            });
        }

        /// <summary>
        /// Allows inspection or modification of request arguments immediately before sending the request.
        /// </summary>
        /// <typeparam name="TArgument">Type or parent type/interface of the argument</typeparam>
        /// <param name="handler">Delegate that takes a <typeparamref name="TArgument"/></param>        
        public void HandleRequestArgument<TArgument>(Action<TArgument> handler)
        {
            this.HandleRequestArgument<TArgument>(null, handler);
        }

        /// <summary>
        /// Allows inspection or modification of request arguments immediately before sending the request.
        /// </summary>
        /// <typeparam name="TArgument">Type or parent type/interface of the argument</typeparam>
        /// <param name="where">Predicate to filter the request arguments by properties of the request, or the parameter name</param>
        /// <param name="handler">Delegate that takes a <typeparamref name="TArgument"/> and returns a <typeparamref name="TArgument"/></param>
        public void HandleRequestArgument<TArgument>(Func<TArgument, string, bool> where, Func<TArgument, TArgument> handler)
        {
            if (!this.requestArgumentHandlers.ContainsKey(typeof(TArgument)))
                this.requestArgumentHandlers.Add(typeof(TArgument), new List<PredicateHandlerHolder>());

            this.requestArgumentHandlers[typeof(TArgument)].Add(new PredicateHandlerHolder
            {
                Predicate = where,
                Handler = handler
            });
        }

        /// <summary>
        /// Allows inspection or modification of request arguments immediately before sending the request.
        /// </summary>
        /// <typeparam name="TArgument">Type or parent type/interface of the argument</typeparam>
        /// <param name="handler">Delegate that takes a <typeparamref name="TArgument"/> and returns a <typeparamref name="TArgument"/></param>
        public void HandleRequestArgument<TArgument>(Func<TArgument, TArgument> handler)
        {
            this.HandleRequestArgument(null, handler);
        }

        #region Runtime Handler Resolution

        /// <summary>
        /// Called into by the dynamically generated proxy
        /// </summary>
        protected TArgument HandleRequestArgument<TArgument>(TArgument argument, string parameterName)
        {
            // Don't attempt handler resolution if there aren't any registered
            if (!this.requestArgumentHandlers.Any())
                return argument;

            argument = ExecuteRequestArgumentHandlers(argument, parameterName);
            return argument;
        }

        private TArgument ExecuteRequestArgumentHandlers<TArgument>(TArgument requestArgument, string parameterName)
        {
            Type @type = typeof(TArgument);
            var baseTypes = TypeHierarchyCache.GetOrAddSafe(@type, _ =>
            {
                return @type.GetAllInheritedTypes();
            });

            foreach (var baseType in baseTypes)
                requestArgument = this.ExecuteRequestArgumentHandlers(requestArgument, parameterName, baseType);

            return requestArgument;
        }

        private TArgument ExecuteRequestArgumentHandlers<TArgument>(TArgument response, string parameterName, Type @type)
        {
            if (!this.requestArgumentHandlers.ContainsKey(@type))
                return response;

            IList<PredicateHandlerHolder> requestParameterHandlerHolders = this.requestArgumentHandlers[@type];

            MethodInfo predicateInvokeMethod = RequestParameterHandlerPredicateCache.GetOrAddSafe(@type, _ =>
            {
                Type predicateType = typeof(Func<,,>)
                    .MakeGenericType(@type, typeof(string), typeof(bool));

                return predicateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            });

            var handlers = requestParameterHandlerHolders
                .Where(m => m.Predicate == null
                            || ((bool) predicateInvokeMethod.Invoke(m.Predicate, new object[] { response, parameterName })))
                .ToList();

            if (!handlers.Any())
                return response;

            MethodInfo handlerMethod = RequestParameterHandlerCache.GetOrAddSafe(@type, _ =>
            {
                Type actionType = typeof(Func<,>).MakeGenericType(@type, @type);
                return actionType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            });

            foreach (var handler in handlers)
            {
                try
                {
                    response = (TArgument) handlerMethod.Invoke(handler.Handler, new object[] { response });
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }
            }

            return response;
        }

        #endregion

        #endregion

        #region HandleResponse

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
            actionInvoker.AddResponseHandler(r =>
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
            actionInvoker.AddResponseHandler<TResponse>(r =>
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
            actionInvoker.AddResponseHandler(handler, null);
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
            actionInvoker.AddResponseHandler(handler, @where);
        }

        #endregion

        #region IRetryingProxyConfigurator

        /// <summary>
        /// Event that is fired immediately before the service method will be called. This event
        /// is called only once per request.
        /// </summary>
        public event OnCallBeginHandler OnCallBegin
        {
            add { actionInvoker.OnCallBegin += value; }
            remove { actionInvoker.OnCallBegin -= value; }
        }

        /// <summary>
        /// Event that is fired immediately after the request successfully or unsuccessfully completes.
        /// </summary>
        public event OnCallSuccessHandler OnCallSuccess
        {
            add { actionInvoker.OnCallSuccess += value; }
            remove { actionInvoker.OnCallSuccess -= value; }
        }

        /// <summary>
        /// Fires before the invocation of a service method, at every retry.
        /// </summary>
        public event OnInvokeHandler OnBeforeInvoke
        {
            add { actionInvoker.OnBeforeInvoke += value; }
            remove { actionInvoker.OnBeforeInvoke -= value; }
        }

        /// <summary>
        /// Fires after the successful invocation of a method.
        /// </summary>
        public event OnInvokeHandler OnAfterInvoke
        {
            add { actionInvoker.OnAfterInvoke += value; }
            remove { actionInvoker.OnAfterInvoke -= value; }
        }

        /// <summary>
        /// Fires after the successful invocation of a method.
        /// </summary>
        public event OnExceptionHandler OnException
        {
            add { actionInvoker.OnException += value; }
            remove { actionInvoker.OnException -= value; }
        }

        /// <summary>
        /// Allows access to WCF extensibility features.
        /// </summary>
        public ChannelFactory ChannelFactory
        {
            get
            {
                // if requested without endpoint set, use default
                if (channelFactory == null)
                {
                    UseDefaultEndpoint();
                }

                return channelFactory;
            }
        }

        public void UseDefaultEndpoint()
        {
            // If TServiceInterface is our generated async interface, the ChannelFactory
            // will look for a config based on the *Async contract. We need to fix this manually.
            if (typeof(TServiceInterface).GetCustomAttribute<GeneratedAsyncInterfaceAttribute>() != null)
            {
                Type originalServiceInterfaceType = typeof(TServiceInterface).GetInterfaces()[0];
                channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(originalServiceInterfaceType);
            }
            else
            {
                channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(typeof(TServiceInterface));    
            }
        }

        public void SetEndpoint(string endpointConfigurationName)
        {
            if (typeof(TServiceInterface).HasAttribute<GeneratedAsyncInterfaceAttribute>())
            {
                Type originalServiceInterfaceType = typeof(TServiceInterface).GetInterfaces()[0];
                channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(endpointConfigurationName, originalServiceInterfaceType);
            }
            else
            {
                channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(endpointConfigurationName);    
            }
        }

        public void SetEndpoint(Binding binding, EndpointAddress endpointAddress)
        {
            channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(binding, endpointAddress);
        }

        public void SetEndpoint(Binding binding, EndpointAddress endpointAddress, object callbackObject)
        {
            channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(binding, endpointAddress, callbackObject);
        }

        public void SetEndpoint(ServiceEndpoint endpoint)
        {
            channelFactory = ChannelFactoryProvider.GetChannelFactory<TServiceInterface>(endpoint);
        }

        public void MaximumRetries(int retryCount)
        {
            actionInvoker.RetryCount = retryCount;
        }

        public void SetDelayPolicy(Func<IDelayPolicy> policyFactory)
        {
            actionInvoker.DelayPolicyFactory = policyFactory;
        }

        public void RetryOnException<TException>(Predicate<TException> where = null)
            where TException : Exception
        {
            actionInvoker.AddExceptionToRetryOn<TException>(where);
        }

        public void RetryOnException(Type exceptionType, Predicate<Exception> where = null)
        {
            actionInvoker.AddExceptionToRetryOn(exceptionType, where);
        }

        public void RetryOnResponse<TResponse>(Predicate<TResponse> where)
        {
            actionInvoker.AddResponseToRetryOn(where);
        }

        public void RetryFailureExceptionFactory(RetryFailureExceptionFactoryDelegate factory)
        {
            actionInvoker.RetryFailureExceptionFactory = factory;
        }

        #endregion
    }
}