using System;
using System.Collections.Concurrent;
using System.ServiceModel;
using System.ServiceModel.Channels;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    internal static class ChannelFactoryProvider
    {
        private static readonly ConcurrentDictionary<string, Lazy<object>> ChannelFactoryCache
            = new ConcurrentDictionary<string, Lazy<object>>();

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>()
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>();
            return GetChannelFactory(cacheKey, () => new ChannelFactory<TServiceInterface>("*"));
        }
        
        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(string endpointConfigurationName)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(endpointConfigurationName);
            return GetChannelFactory(cacheKey, () => new ChannelFactory<TServiceInterface>(endpointConfigurationName));
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(Binding binding, EndpointAddress endpointAddress)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(binding, endpointAddress);
            return GetChannelFactory(cacheKey, () => new ChannelFactory<TServiceInterface>(binding, endpointAddress));
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
            return string.Format("type:{0}", typeof(TServiceInterface).FullName);
        }

        private static string GetCacheKey<TServiceInterface>(string endpointConfigurationName)
        {
            return string.Format("type:{0};config:{1}",
                                 typeof(TServiceInterface).FullName,
                                 endpointConfigurationName);
        }

        private static string GetCacheKey<TServiceInterface>(Binding binding, EndpointAddress endpointAddress)
        {
            return string.Format("type:{0};binding:{1};uri:{2}",
                                 typeof(TServiceInterface).FullName,
                                 binding.Name,
                                 endpointAddress);
        }
    }
}