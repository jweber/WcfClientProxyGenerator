using System;
using System.Collections.Concurrent;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace WcfClientProxyGenerator
{
    internal static class ChannelFactoryProvider
    {
        private static readonly ConcurrentDictionary<string, Lazy<object>> _channelFactoryCache;

        static ChannelFactoryProvider()
        {
            _channelFactoryCache = new ConcurrentDictionary<string, Lazy<object>>();
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(string endpointConfigurationName)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(endpointConfigurationName);
            var channelFactory = GetOrAddToCache(
                cacheKey,
                () => new ChannelFactory<TServiceInterface>(endpointConfigurationName));

            return channelFactory;
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(Binding binding, EndpointAddress endpointAddress)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(binding, endpointAddress);
            var channelFactory = GetOrAddToCache(
                cacheKey,
                () => new ChannelFactory<TServiceInterface>(binding, endpointAddress));

            return channelFactory;
        }

        private static ChannelFactory<TServiceInterface> GetOrAddToCache<TServiceInterface>(
            string cacheKey,
            Func<ChannelFactory<TServiceInterface>> factory)
        {
            Lazy<object> lazy = _channelFactoryCache.GetOrAdd(cacheKey, new Lazy<object>(factory));
            return lazy.Value as ChannelFactory<TServiceInterface>;
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