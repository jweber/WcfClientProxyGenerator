using System;

namespace WcfClientProxyGenerator.Policy
{
    /// <summary>
    /// Defines how the delay between failed WCF calls is calculated
    /// </summary>
    public interface IDelayPolicy
    {
        /// <summary>
        /// Gets the amount of time that failed calls to the WCF
        /// service will delay by
        /// </summary>
        /// <param name="iteration"></param>
        TimeSpan GetDelay(int iteration);
    }
}
