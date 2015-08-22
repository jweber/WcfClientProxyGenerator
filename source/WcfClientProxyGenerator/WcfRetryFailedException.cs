using System;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Thrown when the maximum amount of retried calls is exceeded
    /// </summary>
    public class WcfRetryFailedException : Exception
    {
        internal WcfRetryFailedException(string message) : base(message)
        {}

        internal WcfRetryFailedException(string message, Exception innerException) : base(message, innerException)
        {}
    }
}
