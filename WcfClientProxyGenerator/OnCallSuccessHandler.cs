using System;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Arguments given to the <see cref="OnCallSuccessHandler"/> delegate
    /// </summary>
    public class OnCallSuccessHandlerArguments
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
        /// Amount of tries made to the service before the
        /// request was successful
        /// </summary>
        public int RequestAttempts { get; set; }
    }

    /// <summary>
    /// Callback type used for the OnCallSuccess event
    /// </summary>
    /// <param name="invoker">References the object that fired this event</param>
    /// <param name="args">Event information including method name and service type</param>
    public delegate void OnCallSuccessHandler(object invoker, OnCallSuccessHandlerArguments args);
}