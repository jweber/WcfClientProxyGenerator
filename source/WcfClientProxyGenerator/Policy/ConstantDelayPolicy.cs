using System;

namespace WcfClientProxyGenerator.Policy
{
    /// <summary>
    /// Delays failed calls by a constant amount of time
    /// </summary>
    public class ConstantDelayPolicy : IDelayPolicy
    {
        private readonly TimeSpan delay;

        /// <summary>
        /// Delays failed calls by <paramref name="delay"/>
        /// regardless of the current iteration
        /// </summary>
        /// <param name="delay"></param>
        public ConstantDelayPolicy(TimeSpan delay)
        {
            this.delay = delay;
        }

        public TimeSpan GetDelay(int iteration)
        {
            return this.delay;
        }
    }
}
