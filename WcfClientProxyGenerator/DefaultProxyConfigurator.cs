using System;
using WcfClientProxyGenerator.Policy;

namespace WcfClientProxyGenerator
{
    internal static class DefaultProxyConfigurator
    {
        public static void Configure<TServiceInterface>(IRetryingProxyConfigurator proxy)
            where TServiceInterface : class
        {
#if NETFULL
            proxy.UseDefaultEndpoint();
#endif
        }

        public static readonly Func<LinearBackoffDelayPolicy> DefaultDelayPolicyFactory
            = () => new LinearBackoffDelayPolicy(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10));

        public static readonly RetryFailureExceptionFactoryDelegate DefaultRetryFailureExceptionFactory
            = (retryCount, lastException, invokeInfo) => new WcfRetryFailedException(string.Format("WCF call failed after {0} retries.", retryCount), lastException);
    }
}