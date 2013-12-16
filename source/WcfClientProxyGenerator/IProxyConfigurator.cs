﻿using System.ServiceModel;
using System.ServiceModel.Channels;

namespace WcfClientProxyGenerator
{
    public interface IProxyConfigurator
    {
        void UseDefaultEndpoint();
        void SetEndpoint(string endpointConfigurationName);
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
    }
}