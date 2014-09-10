using System;

namespace WcfClientProxyGenerator
{
    public class OnCallEndHandlerArguments
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
    }

    /// <summary>
    /// Callback type used for the OnCallEnd event
    /// </summary>
    /// <param name="invoker">References the object that fired this event</param>
    /// <param name="args">Event information including method name and service type</param>
    public delegate void OnCallEndHandler(object invoker, OnCallEndHandlerArguments args);
}