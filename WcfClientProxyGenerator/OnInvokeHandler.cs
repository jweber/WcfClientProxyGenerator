﻿using System;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Contains information about the invoked method.
    /// </summary>
    public class OnInvokeHandlerArguments
    {
        /// <summary>
        /// Information on which method was invoked and with what parameters.
        /// </summary>
        public InvokeInfo InvokeInfo { get; set; }

        /// <summary>
        /// Type of the service that was invoked.
        /// </summary>
        public Type ServiceType { get; set; }
        
        /// <summary>
        /// Duration of the call
        /// </summary>
        public TimeSpan CallDuration { get; set; }

        /// <summary>
        /// True if this is a re-try of a service method invocation.
        /// <seealso cref="IRetryingProxyConfigurator" />
        /// </summary>
        public bool IsRetry
        {
            get
            {
                return RetryCounter > 0;
            }
        }

        /// <summary>
        /// How many times this method has been tried to be invoked.
        /// <seealso cref="IRetryingProxyConfigurator" />
        /// </summary>
        public int RetryCounter { get; set; }
    }

    /// <summary>
    /// Callback type used for the OnBeforeInvoke and OnAfterInvoke methods.
    /// </summary>
    /// <param name="invoker">References the object that fired this event</param>
    /// <param name="args">Event information including method name and service type</param>
    public delegate void OnInvokeHandler(object invoker, OnInvokeHandlerArguments args);
}