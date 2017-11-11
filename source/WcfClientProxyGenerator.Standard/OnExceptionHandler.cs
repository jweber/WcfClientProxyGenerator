using System;

namespace WcfClientProxyGenerator.Standard
{
    /// <summary>
    /// Contains information about the exception that happened
    /// and the method invocation.
    /// </summary>
    public class OnExceptionHandlerArguments : OnInvokeHandlerArguments
    {
        /// <summary>
        /// Exception object
        /// </summary>
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Callback type used for the OnException method.
    /// </summary>
    /// <param name="invoker">References the object that fired this event</param>
    /// <param name="args">Event information including method name, service type and the exception</param>
    public delegate void OnExceptionHandler(object invoker, OnExceptionHandlerArguments args);
}
