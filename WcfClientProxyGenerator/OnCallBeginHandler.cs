using System;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Arguments given to the <see cref="OnCallBeginHandler"/> delegate
    /// </summary>
    public class OnCallBeginHandlerArguments
    {
        /// <summary>
        /// Information on which method was invoked and with what parameters.
        /// </summary>
        public InvokeInfo InvokeInfo { get; set; }

        /// <summary>
        /// Type of the service that was invoked.
        /// </summary>
        public Type ServiceType { get; set; }
    }

    /// <summary>
    /// Callback type used for the OnCallBegin event
    /// </summary>
    /// <param name="invoker">References the object that fired this event</param>
    /// <param name="args">Event information including method name and service type</param>
    public delegate void OnCallBeginHandler(object invoker, OnCallBeginHandlerArguments args);
}