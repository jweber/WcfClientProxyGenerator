using System;

namespace WcfClientProxyGenerator.Policy
{
    /// <summary>
    /// Delays failed calls by an amount of time that
    /// grows exponentially (2^n)
    /// </summary>
    public class ExponentialBackoffDelayPolicy : IDelayPolicy
    {
        private readonly TimeSpan minimumDelay;
        private readonly TimeSpan maximumDelay;

        /// <summary>
        /// Delays failed calls by an amount of time starting
        /// at <paramref name="minimumDelay"/> that grows exponentially
        /// based on the iteration count
        /// </summary>
        /// <param name="minimumDelay"></param>
        public ExponentialBackoffDelayPolicy(TimeSpan minimumDelay)
            : this(minimumDelay, TimeSpan.MaxValue)
        {}

        /// <summary>
        /// Delays failed calls by an amount of time starting
        /// at <paramref name="minimumDelay"/> that grows exponentially
        /// based on the iteration count. The maximum amount of time that a single
        /// iteration will delay is defined by <paramref name="maximumDelay"/>
        /// </summary>
        /// <param name="minimumDelay"></param>
        /// <param name="maximumDelay"></param>
        public ExponentialBackoffDelayPolicy(TimeSpan minimumDelay, TimeSpan maximumDelay)
        {
            this.minimumDelay = minimumDelay;
            this.maximumDelay = maximumDelay;
        }

        /// <summary>
        /// Gets the amount of time that failed calls to the WCF
        /// service will delay by
        /// </summary>
        /// <param name="iteration"></param>
        public TimeSpan GetDelay(int iteration)
        {
            double delay = Math.Pow(2d, iteration) * this.minimumDelay.TotalMilliseconds;

            return delay < this.maximumDelay.TotalMilliseconds
                ? TimeSpan.FromMilliseconds(delay)
                : this.maximumDelay;
        }
    }
}
