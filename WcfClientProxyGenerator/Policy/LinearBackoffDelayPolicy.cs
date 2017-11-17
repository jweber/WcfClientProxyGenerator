using System;

namespace WcfClientProxyGenerator.Policy
{
    /// <summary>
    /// Delays failed calls by an amount of time that
    /// grows linearly (i*n)
    /// </summary>
    public class LinearBackoffDelayPolicy : IDelayPolicy
    {
        private readonly TimeSpan minimumDelay;
        private readonly TimeSpan maximumDelay;
        
        /// <summary>
        /// Delays failed calls by an amount of time starting
        /// at <paramref name="minimumDelay"/> that grows linearly
        /// based on the iteration count
        /// </summary>
        /// <param name="minimumDelay"></param>
        public LinearBackoffDelayPolicy(TimeSpan minimumDelay)
            : this(minimumDelay, TimeSpan.MaxValue)
        {}

        /// <summary>
        /// Delays failed calls by an amount of time starting
        /// at <paramref name="minimumDelay"/> that grows linearly
        /// based on the iteration count. The maximum amount of time that a single
        /// iteration will delay is defined by <paramref name="maximumDelay"/>
        /// </summary>
        /// <param name="minimumDelay"></param>
        /// <param name="maximumDelay"></param>
        public LinearBackoffDelayPolicy(TimeSpan minimumDelay, TimeSpan maximumDelay)
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
            var delay = TimeSpan.FromMilliseconds(this.minimumDelay.TotalMilliseconds * (iteration + 1));
            
            return delay < this.maximumDelay
                ? delay
                : this.maximumDelay;
        }
    }
}