using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace WcfClientProxyGenerator
{
    public interface IProxyConfigurator
    {
        /// <summary>
        /// Uses the default endpoint configuration from the web.config or app.config
        /// </summary>
        void UseDefaultEndpoint();

        /// <summary>
        /// Specifies the endpoint configuration to use
        /// </summary>
        /// <param name="endpointConfigurationName"></param>
        void SetEndpoint(string endpointConfigurationName);

        /// <summary>
        /// Specifies the binding and address to use
        /// </summary>
        /// <param name="binding"></param>
        /// <param name="endpointAddress"></param>
        void SetEndpoint(Binding binding, EndpointAddress endpointAddress);

        /// <summary>
        /// Specifies the binding, address to use and callbackInstance to use with DuplexChannel
        /// </summary>
        /// <param name="binding"></param>
        /// <param name="endpointAddress"></param>
        /// <param name="callbackInstance"></param>
        void SetEndpoint(Binding binding, EndpointAddress endpointAddress, object callbackInstance);

        /// <summary>
        /// Specifies the endpoint configuration to use.
        /// </summary>
        /// <param name="endpoint"></param>
        void SetEndpoint(ServiceEndpoint endpoint);

        /// <summary>
        /// Event that is fired immediately before the service method will be called. This event
        /// is called only once per request.
        /// </summary>
        event OnCallBeginHandler OnCallBegin;

        /// <summary>
        /// Event that is fired immediately after the request successfully completes.
        /// </summary>
        event OnCallSuccessHandler OnCallSuccess;

        /// <summary>
        /// Event that is fired when the method is about to be called.
        /// The event is fired for every attempt to call the service method.
        /// </summary>
        event OnInvokeHandler OnBeforeInvoke;

        /// <summary>
        /// Event that is fired after the service method has been called.
        /// This event fires only when the method has been successfully called.
        /// </summary>
        event OnInvokeHandler OnAfterInvoke;

        /// <summary>
        /// Event that is fired if the service call fails with an exception.
        /// This event is fired for every failed attempt to call the service method.
        /// </summary>
        event OnExceptionHandler OnException;

        /// <summary>
        /// Allows access to WCF extensibility features.
        /// </summary>
        /// <remarks>
        /// Make sure this is called after any other endpoint-modifying configuration operations,
        /// as not doing so will not produce expected results.
        /// </remarks>
        ChannelFactory ChannelFactory { get; }

        #region HandleRequestArgument

        /// <summary>
        /// Allows inspection or modification of request arguments immediately before sending the request.
        /// </summary>
        /// <typeparam name="TArgument">Type or parent type/interface of the argument</typeparam>
        /// <param name="where">Predicate to filter the request arguments by properties of the request, or the parameter name</param>
        /// <param name="handler">Delegate that takes a <typeparamref name="TArgument"/></param>
        void HandleRequestArgument<TArgument>(Func<TArgument, string, bool> where, Action<TArgument> handler);

        /// <summary>
        /// Allows inspection or modification of request arguments immediately before sending the request.
        /// </summary>
        /// <typeparam name="TArgument">Type or parent type/interface of the argument</typeparam>
        /// <param name="handler">Delegate that takes a <typeparamref name="TArgument"/></param>        
        void HandleRequestArgument<TArgument>(Action<TArgument> handler);

        /// <summary>
        /// Allows inspection or modification of request arguments immediately before sending the request.
        /// </summary>
        /// <typeparam name="TArgument">Type or parent type/interface of the argument</typeparam>
        /// <param name="where">Predicate to filter the request arguments by properties of the request, or the parameter name</param>
        /// <param name="handler">Delegate that takes a <typeparamref name="TArgument"/> and returns a <typeparamref name="TArgument"/></param>
        void HandleRequestArgument<TArgument>(Func<TArgument, string, bool> where, Func<TArgument, TArgument> handler);
        
        /// <summary>
        /// Allows inspection or modification of request arguments immediately before sending the request.
        /// </summary>
        /// <typeparam name="TArgument">Type or parent type/interface of the argument</typeparam>
        /// <param name="handler">Delegate that takes a <typeparamref name="TArgument"/> and returns a <typeparamref name="TArgument"/></param>
        void HandleRequestArgument<TArgument>(Func<TArgument, TArgument> handler);

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
        void HandleResponse<TResponse>(Predicate<TResponse> where, Action<TResponse> handler);

        /// <summary>
        /// Allows inspecting and modifying the <typeparamref name="TResponse"/> object
        /// before returning the response to the calling method.
        /// </summary>
        /// <typeparam name="TResponse">Type or parent type/interface of the response</typeparam>
        /// <param name="handler">
        /// Delegate that takes a <typeparamref name="TResponse"/>
        /// </param>    
        void HandleResponse<TResponse>(Action<TResponse> handler);

        /// <summary>
        /// Allows inspecting and modifying the <typeparamref name="TResponse"/> object
        /// before returning the response to the calling method.
        /// </summary>
        /// <typeparam name="TResponse">Type or parent type/interface of the response</typeparam>
        /// <param name="where">Predicate to filter responses based on its parameters</param>
        /// <param name="handler">
        /// Delegate that takes a <typeparamref name="TResponse"/> and returns a <typeparamref name="TResponse"/>
        /// </param>
        void HandleResponse<TResponse>(Predicate<TResponse> where, Func<TResponse, TResponse> handler);

        /// <summary>
        /// Allows inspecting and modifying the <typeparamref name="TResponse"/> object
        /// before returning the response to the calling method.
        /// </summary>
        /// <typeparam name="TResponse">Type or parent type/interface of the response</typeparam>
        /// <param name="handler">
        /// Delegate that takes a <typeparamref name="TResponse"/> and returns a <typeparamref name="TResponse"/>
        /// </param>        
        void HandleResponse<TResponse>(Func<TResponse, TResponse> handler);

        #endregion
    }
}