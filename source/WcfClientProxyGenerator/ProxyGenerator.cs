using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator
{
    public static class ProxyGenerator
    {
        private static readonly ConcurrentDictionary<Type, Type> ProxyCache = new ConcurrentDictionary<Type, Type>();

        public static TServiceInterface Create<TServiceInterface>(string endpointConfigurationName, Action<IProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>(endpointConfigurationName);

            if (configurator != null)
            {
                configurator(proxy as IProxyConfigurator);
            }

            return proxy;            
        }

        public static TServiceInterface Create<TServiceInterface>(Binding binding, EndpointAddress endpointAddress, Action<IProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>(binding, endpointAddress);
            
            if (configurator != null)
            {
                configurator(proxy as IProxyConfigurator);
            }

            return proxy;
        }

        private static TServiceInterface CreateProxy<TServiceInterface>(params object[] arguments)
            where TServiceInterface : class
        {
            var proxyType = GetProxyType<TServiceInterface>();
            return (TServiceInterface) Activator.CreateInstance(proxyType, arguments);
        }

        private static Type GetProxyType<TServiceInterface>()
            where TServiceInterface : class
        {
            return ProxyCache.GetOrAdd(
                typeof(TServiceInterface), 
                _ => InternalProxyGenerator<TServiceInterface>.GenerateType());
        }  
    }
}
