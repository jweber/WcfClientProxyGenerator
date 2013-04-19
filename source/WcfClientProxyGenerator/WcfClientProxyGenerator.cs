using System;
using System.Collections.Concurrent;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    public static class WcfClientProxyGenerator
    {
        private static readonly ConcurrentDictionary<Type, Lazy<Type>> ProxyCache 
            = new ConcurrentDictionary<Type, Lazy<Type>>();

        public static TServiceInterface Create<TServiceInterface>(Action<IRetryingProxyConfigurator> configurator)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>();

            if (configurator != null)
            {
                configurator(proxy as IRetryingProxyConfigurator);
            }

            return proxy;            
        }

        private static TServiceInterface CreateProxy<TServiceInterface>(params object[] arguments)
            where TServiceInterface : class
        {
            var proxyType = GetProxyType<TServiceInterface>();
            return (TServiceInterface) FastActivator.CreateInstance(proxyType);
        }

        private static Type GetProxyType<TServiceInterface>()
            where TServiceInterface : class
        {
            return ProxyCache.GetOrAddSafe(
                typeof(TServiceInterface), 
                _ => DynamicProxyTypeGenerator<TServiceInterface>.GenerateType());
        }  
    }
}
