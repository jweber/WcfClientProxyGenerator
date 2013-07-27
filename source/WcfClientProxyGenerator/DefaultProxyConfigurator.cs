using System;
using WcfClientProxyGenerator.Policy;

namespace WcfClientProxyGenerator
{
    internal static class DefaultProxyConfigurator
    {
        public static void Configure<TServiceInterface>(IRetryingProxyConfigurator proxy)
        {
            proxy.UseDefaultEndpoint();
        }

        public static readonly Func<LinearBackoffDelayPolicy> DefaultDelayPolicyFactory
            = () => new LinearBackoffDelayPolicy(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10));
    }
}