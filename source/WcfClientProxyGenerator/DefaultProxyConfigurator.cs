namespace WcfClientProxyGenerator
{
    internal static class DefaultProxyConfigurator
    {
        public static void Configure<TServiceInterface>(IRetryingProxyConfigurator proxy)
        {
            proxy.SetEndpoint(typeof(TServiceInterface).FullName);
        }
    }
}