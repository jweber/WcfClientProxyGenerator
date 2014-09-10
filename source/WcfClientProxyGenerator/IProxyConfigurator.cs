using System.ServiceModel;
using System.ServiceModel.Channels;

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
    }
}