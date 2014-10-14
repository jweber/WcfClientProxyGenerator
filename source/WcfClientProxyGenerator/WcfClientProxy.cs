using System;
using System.Collections.Concurrent;
using WcfClientProxyGenerator.Async;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Factory class for generating WCF clients
    /// </summary>
    public static class WcfClientProxy
    {
        private static readonly ConcurrentDictionary<Type, Lazy<GeneratedTypes>> ProxyCache 
            = new ConcurrentDictionary<Type, Lazy<GeneratedTypes>>();

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>()
            where TServiceInterface : class
        {
            return Create<TServiceInterface>((Action<IRetryingProxyConfigurator>)null);
        }

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="endpointConfigurationName">Name of the WCF service configuration</param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>(string endpointConfigurationName)
            where TServiceInterface : class
        {
            return Create<TServiceInterface>(c => c.SetEndpoint(endpointConfigurationName));
        }

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>(Action<IRetryingProxyConfigurator> configurator)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<
                TServiceInterface,
                RetryingWcfActionInvokerProvider<TServiceInterface>>();

            if (configurator != null)
            {
                configurator(proxy as IRetryingProxyConfigurator);
            }
            else
            {
                DefaultProxyConfigurator.Configure<TServiceInterface>(proxy as IRetryingProxyConfigurator);
            }

            return proxy;            
        }

        #region Async Proxy

        /// <summary>
        /// Creates a wrapper for calling synchronously defined WCF methods asynchronously.
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsyncProxy<TServiceInterface>()
            where TServiceInterface : class
        {
            return CreateAsyncProxy<TServiceInterface>(c => c.UseDefaultEndpoint());
        }

        /// <summary>
        /// Creates a wrapper for calling synchronously defined WCF methods asynchronously.
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="endpointConfigurationName">Name of the WCF service configuration</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsyncProxy<TServiceInterface>(string endpointConfigurationName)
            where TServiceInterface : class
        {
            return CreateAsyncProxy<TServiceInterface>(c => c.SetEndpoint(endpointConfigurationName));
        }

        /// <summary>
        /// Creates a wrapper for calling synchronously defined WCF methods asynchronously.
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsyncProxy<TServiceInterface>(
            Action<IRetryingProxyConfigurator> configurator)
            where TServiceInterface : class
        {
            var proxy = Create<TServiceInterface>(configurator);
            return new AsyncProxy<TServiceInterface>(proxy);
        }

        #endregion
        
        private static TServiceInterface CreateProxy<TServiceInterface, TActionInvokerProvider>()
            where TServiceInterface : class
            where TActionInvokerProvider : IActionInvokerProvider<TServiceInterface>
        {
            var types = GetGeneratedTypes<TServiceInterface, TActionInvokerProvider>();
            return (TServiceInterface) FastActivator.CreateInstance(types.Proxy);
        }

        private static GeneratedTypes GetGeneratedTypes<TServiceInterface, TActionInvokerProvider>()
            where TServiceInterface : class
            where TActionInvokerProvider : IActionInvokerProvider<TServiceInterface>
        {
            return ProxyCache.GetOrAddSafe(
                typeof(TServiceInterface), 
                _ => DynamicProxyTypeGenerator<TServiceInterface>.GenerateTypes<TActionInvokerProvider>());
        }  
    }
}
