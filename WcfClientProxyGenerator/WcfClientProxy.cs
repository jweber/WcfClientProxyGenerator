using System;
using System.Collections.Concurrent;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
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
        /// <param name="binding"></param>
        /// <param name="endpointAddress"></param>
        /// <param name="configurator"></param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>(
            Binding binding, 
            EndpointAddress endpointAddress, 
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            return CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(binding, endpointAddress);

                configurator?.Invoke(c);
            });
        }

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="binding"></param>
        /// <param name="endpointAddress"></param>
        /// <param name="instanceContext"></param>
        /// <param name="configurator"></param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>(
            Binding binding, 
            EndpointAddress endpointAddress, 
            InstanceContext instanceContext,
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            return CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(binding, endpointAddress, instanceContext);

                configurator?.Invoke(c);
            });
        }

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <typeparam name="TCallback"></typeparam>
        /// <param name="binding"></param>
        /// <param name="endpointAddress"></param>
        /// <param name="instanceContext"></param>
        /// <param name="configurator"></param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface, TCallback>(
            Binding binding, 
            EndpointAddress endpointAddress, 
            InstanceContext<TCallback> instanceContext,
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            return CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(binding, endpointAddress, instanceContext);

                configurator?.Invoke(c);
            });
        }
        
        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>(
            ServiceEndpoint serviceEndpoint,
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            return CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(serviceEndpoint);

                configurator?.Invoke(c);
            });
        }

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <typeparam name="TCallback"></typeparam>
        /// <param name="serviceEndpoint"></param>
        /// <param name="instanceContext"></param>
        /// <param name="configurator"></param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface, TCallback>(
            ServiceEndpoint serviceEndpoint,
            InstanceContext<TCallback> instanceContext, 
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            return CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(serviceEndpoint, instanceContext);

                configurator?.Invoke(c);
            });
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
            => CreateProxy<TServiceInterface>(configurator);
        
#if NETFULL

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>()
            where TServiceInterface : class 
            => Create<TServiceInterface>((Action<IRetryingProxyConfigurator>)null);

        /// <summary>
        /// Creates a proxy instance of <typeparamref name="TServiceInterface"/> that
        /// is used to initiate calls to a WCF service.
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="endpointConfigurationName">Name of the WCF service configuration</param>
        /// <returns>The WCF client proxy</returns>
        public static TServiceInterface Create<TServiceInterface>(string endpointConfigurationName)
            where TServiceInterface : class 
            => Create<TServiceInterface>(c => c.SetEndpoint(endpointConfigurationName));

#endif

        private static TServiceInterface CreateProxy<TServiceInterface>(Action<IRetryingProxyConfigurator> configurator)
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
        /// <param name="binding"></param>
        /// <param name="endpointAddress"></param>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsyncProxy<TServiceInterface>(
            Binding binding,
            EndpointAddress endpointAddress,
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(binding, endpointAddress);
                configurator?.Invoke(c);
            });
            
            return new AsyncProxy<TServiceInterface>(proxy);
        }

        /// <summary>
        /// Creates a wrapper for calling synchronously defined WCF methods asynchronously.
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="binding"></param>
        /// <param name="endpointAddress"></param>
        /// <param name="instanceContext"></param>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsyncProxy<TServiceInterface>(
            Binding binding,
            EndpointAddress endpointAddress,
            InstanceContext instanceContext,
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(binding, endpointAddress, instanceContext);
                configurator?.Invoke(c);
            });
            
            return new AsyncProxy<TServiceInterface>(proxy);
        }

        /// <summary>
        /// Creates a wrapper for calling synchronously defined WCF methods asynchronously.
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <typeparam name="TCallback"></typeparam>
        /// <param name="binding"></param>
        /// <param name="endpointAddress"></param>
        /// <param name="instanceContext"></param>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsyncProxy<TServiceInterface, TCallback>(
            Binding binding,
            EndpointAddress endpointAddress,
            InstanceContext<TCallback> instanceContext,
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(binding, endpointAddress, instanceContext);
                configurator?.Invoke(c);
            });
            
            return new AsyncProxy<TServiceInterface>(proxy);
        }

        /// <summary>
        /// Creates a wrapper for calling synchronously defined WCF methods asynchronously.
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <param name="serviceEndpoint"></param>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsyncProxy<TServiceInterface>(
            ServiceEndpoint serviceEndpoint,
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(serviceEndpoint);
                configurator?.Invoke(c);
            });
            
            return new AsyncProxy<TServiceInterface>(proxy);
        }
        
        /// <summary>
        /// Creates a wrapper for calling synchronously defined WCF methods asynchronously.
        /// <para>
        /// Synchronous calls can still be made via the <see cref="IAsyncProxy{TServiceInterface}.Client"/>
        /// property.
        /// </para>
        /// </summary>
        /// <typeparam name="TServiceInterface">Interface of the WCF service</typeparam>
        /// <typeparam name="TCallback"></typeparam>
        /// <param name="serviceEndpoint"></param>
        /// <param name="instanceContext"></param>
        /// <param name="configurator">Lambda that defines how the proxy is configured</param>
        /// <returns>Async friendly wrapper around <typeparamref name="TServiceInterface"/></returns>
        public static IAsyncProxy<TServiceInterface> CreateAsyncProxy<TServiceInterface, TCallback>(
            ServiceEndpoint serviceEndpoint,
            InstanceContext<TCallback> instanceContext,
            Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var proxy = CreateProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(serviceEndpoint, instanceContext);
                configurator?.Invoke(c);
            });
            
            return new AsyncProxy<TServiceInterface>(proxy);
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

#if NETFULL

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
            => CreateAsyncProxy<TServiceInterface>(c => { });

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
            => CreateAsyncProxy<TServiceInterface>(c => c.SetEndpoint(endpointConfigurationName));

#endif
        
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
