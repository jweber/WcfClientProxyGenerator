using System;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Thrown when the maximum amount of retried calls is exceeded
    /// </summary>
    public class WcfRetryFailedException : Exception
    {
        public WcfRetryFailedException(string message) : base(message)
        {}

        public WcfRetryFailedException(string message, Exception innerException) : base(message, innerException)
        {}
    }
}
