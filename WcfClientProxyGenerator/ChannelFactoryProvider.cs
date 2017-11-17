using System;
using System.Collections.Concurrent;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using WcfClientProxyGenerator.Util;

#if NETFULL
using System.ServiceModel.Configuration;
using WcfClientProxyGenerator.Configuration;
#endif

namespace WcfClientProxyGenerator
{
    internal static class ChannelFactoryProvider
    {
        private static readonly ConcurrentDictionary<string, Lazy<object>> ChannelFactoryCache
            = new ConcurrentDictionary<string, Lazy<object>>();

#if NETFULL

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(Type originalServiceInterfaceType = null)
            where TServiceInterface : class
        {
            if (originalServiceInterfaceType == null)
                originalServiceInterfaceType = typeof(TServiceInterface);

            string cacheKey = GetCacheKey<TServiceInterface>();
            return GetChannelFactory(cacheKey, () =>
            {
                var clientEndpointConfig = ConfigurationHelper.GetClientEndpointConfiguration<TServiceInterface>(originalServiceInterfaceType);
                return new ChannelFactory<TServiceInterface>(clientEndpointConfig);
            });
        }
        
        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(string endpointConfigurationName, Type originalServiceInterfaceType = null)
            where TServiceInterface : class
        {
            if (originalServiceInterfaceType == null)
                originalServiceInterfaceType = typeof(TServiceInterface);

            string cacheKey = GetCacheKey<TServiceInterface>(endpointConfigurationName);
            return GetChannelFactory(cacheKey, () =>
            {
                var clientEndpointConfig = ConfigurationHelper.GetClientEndpointConfiguration<TServiceInterface>(originalServiceInterfaceType, endpointConfigurationName);
                return new ChannelFactory<TServiceInterface>(clientEndpointConfig);
            });
        }

#endif

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(Binding binding, EndpointAddress endpointAddress)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(binding, endpointAddress);
            return GetChannelFactory(cacheKey, () => new ChannelFactory<TServiceInterface>(binding, endpointAddress));
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(Binding binding, EndpointAddress endpointAddress, InstanceContext callbackObject)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(binding, endpointAddress, callbackObject.GetType());
            return GetChannelFactory(cacheKey, () => new DuplexChannelFactory<TServiceInterface>(callbackObject, binding, endpointAddress));
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface, TCallback>(Binding binding, EndpointAddress endpointAddress, InstanceContext<TCallback> instanceContext)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(binding, endpointAddress, typeof(TCallback));
            return GetChannelFactory(cacheKey, () => new DuplexChannelFactory<TServiceInterface>(instanceContext.Context, binding, endpointAddress));
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(ServiceEndpoint endpoint)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(endpoint);
            return GetChannelFactory(cacheKey, () =>
            {
                endpoint.Contract = ContractDescription.GetContract(typeof(TServiceInterface));
                return new ChannelFactory<TServiceInterface>(endpoint.Binding, endpoint.Address);
            });
        }

         public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(ServiceEndpoint endpoint, InstanceContext callbackObject)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(endpoint, callbackObject.GetType());
            return GetChannelFactory(cacheKey, () =>
            {
                endpoint.Contract = ContractDescription.GetContract(typeof(TServiceInterface));
                return new DuplexChannelFactory<TServiceInterface>(callbackObject, endpoint.Binding, endpoint.Address);
            });
        }

           public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface, TCallback>(ServiceEndpoint endpoint, InstanceContext<TCallback> instanceContext)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(endpoint, typeof(TCallback));
            return GetChannelFactory(cacheKey, () => new DuplexChannelFactory<TServiceInterface>(instanceContext.Context, endpoint.Binding, endpoint.Address));
        }

        private static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(string cacheKey, Func<ChannelFactory<TServiceInterface>> factory)
            where TServiceInterface : class
        {
            var channelFactory = ChannelFactoryCache.GetOrAddSafe(
                cacheKey,
                _ => factory());

            return channelFactory as ChannelFactory<TServiceInterface>;
        }

        private static string GetCacheKey<TServiceInterface>()
        {
            return $"type:{typeof (TServiceInterface).FullName}";
        }

        private static string GetCacheKey<TServiceInterface>(string endpointConfigurationName)
        {
            return $"type:{typeof (TServiceInterface).FullName};config:{endpointConfigurationName}";
        }

        private static string GetCacheKey<TServiceInterface>(Binding binding, EndpointAddress endpointAddress)
        {
            return $"type:{typeof (TServiceInterface).FullName};binding:{binding.Name};uri:{endpointAddress}";
        }

        private static string GetCacheKey<TServiceInterface>(Binding binding, EndpointAddress endpointAddress, Type callbackType)
        {
            string nonDuplexKey = GetCacheKey<TServiceInterface>(binding, endpointAddress);
            return nonDuplexKey + $";callback:{callbackType.FullName}";
        }

        private static string GetCacheKey<TServiceInterface>(ServiceEndpoint endpoint)
        {
            return GetCacheKey<TServiceInterface>(endpoint.Binding, endpoint.Address);
        }

         private static string GetCacheKey<TServiceInterface>(ServiceEndpoint endpoint, Type callbackType)
        {
            string nonDuplexKey = GetCacheKey<TServiceInterface>(endpoint);
            return nonDuplexKey + $";callback:{callbackType.FullName}";
        }
    }
}