using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    internal static class ChannelFactoryProvider
    {
        private static readonly ConcurrentDictionary<string, Lazy<object>> ChannelFactoryCache
            = new ConcurrentDictionary<string, Lazy<object>>();

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(Type originalServiceInterfaceType = null)
            where TServiceInterface : class
        {
            if (originalServiceInterfaceType == null)
                originalServiceInterfaceType = typeof(TServiceInterface);

            string cacheKey = GetCacheKey<TServiceInterface>();
            return GetChannelFactory(cacheKey, () =>
            {
                var clientEndpointConfig = GetClientEndpointConfiguration(originalServiceInterfaceType);
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
                var clientEndpointConfig = GetClientEndpointConfiguration(originalServiceInterfaceType, endpointConfigurationName);
                return new ChannelFactory<TServiceInterface>(clientEndpointConfig);
            });
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(Binding binding, EndpointAddress endpointAddress)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(binding, endpointAddress);
            return GetChannelFactory(cacheKey, () => new ChannelFactory<TServiceInterface>(binding, endpointAddress));
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(Binding binding, EndpointAddress endpointAddress, object callbackObject)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(binding, endpointAddress);
            return GetChannelFactory(cacheKey, () => new DuplexChannelFactory<TServiceInterface>(callbackObject, binding, endpointAddress));
        }

        public static ChannelFactory<TServiceInterface> GetChannelFactory<TServiceInterface>(ServiceEndpoint endpoint)
            where TServiceInterface : class
        {
            string cacheKey = GetCacheKey<TServiceInterface>(endpoint);
            return GetChannelFactory(cacheKey, () => new ChannelFactory<TServiceInterface>(endpoint));
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

        private static string GetCacheKey<TServiceInterface>(ServiceEndpoint endpoint)
        {
            return GetCacheKey<TServiceInterface>(endpoint.Binding, endpoint.Address);
        }

        private static ServiceEndpoint GetClientEndpointConfiguration(
            Type serviceInterfaceType, 
            string endpointConfigurationName = null)
        {
            var configurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (string.IsNullOrEmpty(configurationFile))
                throw new InvalidOperationException(
                    "Could not determine the current configuration files being used in the AppDomain");

            var mappedConfigurationFile = new ExeConfigurationFileMap { ExeConfigFilename = configurationFile };
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(mappedConfigurationFile, ConfigurationUserLevel.None);

            if (configuration == null)
                throw new InvalidOperationException(
                    "Could not load the default configuration file. Unable to locate the default configuration for service type: " +
                    serviceInterfaceType.Name);

            var serviceModelSection = configuration.GetSectionGroup("system.serviceModel") as ServiceModelSectionGroup;
            if (serviceModelSection == null)
                throw new InvalidOperationException("Could not find system.serviceModel section group in the configuration file.");

            var endpoint = GetDefaultEndpointForServiceType(serviceInterfaceType, endpointConfigurationName, serviceModelSection.Client.Endpoints);
            var binding = GetClientEndpointBinding(serviceInterfaceType, endpoint, serviceModelSection.Bindings.BindingCollections);

            var serviceEndpoint = new ServiceEndpoint(ContractDescription.GetContract(serviceInterfaceType))
            {
                Binding = binding,
                Address = new EndpointAddress(endpoint.Address)
            };

            foreach (var behavior in GetEndpointBehaviors(endpoint, serviceModelSection))
                serviceEndpoint.Behaviors.Add(behavior);

            return serviceEndpoint;
        }

        private static ChannelEndpointElement GetDefaultEndpointForServiceType(
            Type serviceInterfaceType, 
            string endpointConfigurationName, 
            ChannelEndpointElementCollection endpoints)
        {
            var endpointsForServiceType = endpoints.Cast<ChannelEndpointElement>()
                .Where(e => e.Contract == serviceInterfaceType.FullName || serviceInterfaceType.FullName.EndsWith(e.Contract))
                .ToList();

            if (!string.IsNullOrEmpty(endpointConfigurationName))
                endpointsForServiceType = endpointsForServiceType.Where(e => e.Name == endpointConfigurationName).ToList();

            if (endpointsForServiceType.Count == 0)
            {
                string message = string.Format(
                    "Could not find default endpoint element that references contract '{0}' in the ServiceModel client configuration section. This might be because no configuration file was found for your application, or because no endpoint element matching this contract could be found in the client element.",
                    serviceInterfaceType.FullName);

                throw new InvalidOperationException(message);
            }

            if (endpointsForServiceType.Count > 1)
            {
                string message = string.Format(
                    "An endpoint configuration section for contract '{0}' could not be loaded because more than one endpoint configuration for that contract was found. Please indicate the preferred endpoint configuration section by name.",
                    serviceInterfaceType.FullName);

                throw new InvalidOperationException(message);
            }

            return endpointsForServiceType[0];
        }

        private static Binding GetClientEndpointBinding(
            Type serviceInterfaceType, 
            ChannelEndpointElement endpoint, 
            IEnumerable<BindingCollectionElement> bindings)
        {
            foreach (var binding in bindings.Where(b => b.BindingName == endpoint.Binding))
            {
                var bindingInstance = (Binding) Activator.CreateInstance(binding.BindingType);

                var configuration = binding.ConfiguredBindings.SingleOrDefault(cb => cb.Name == endpoint.BindingConfiguration);
                if (configuration != null)
                {
                    bindingInstance.Name = configuration.Name;
                    configuration.ApplyConfiguration(bindingInstance);
                }

                return bindingInstance;
            }

            var message = string.Format("Could not determine binding from configuration section for contract '{0}'", serviceInterfaceType.FullName);
            throw new InvalidOperationException(message);
        }

        private static IEnumerable<IEndpointBehavior> GetEndpointBehaviors(
            ChannelEndpointElement endpoint,
            ServiceModelSectionGroup serviceModelSectionGroup)

        {
            if (string.IsNullOrEmpty(endpoint.BehaviorConfiguration) || serviceModelSectionGroup.Behaviors == null || serviceModelSectionGroup.Behaviors.EndpointBehaviors.Count == 0)
                yield break;


            var behaviorCollectionElement = serviceModelSectionGroup.Behaviors.EndpointBehaviors[endpoint.BehaviorConfiguration];
            foreach (var behaviorExtension in behaviorCollectionElement)

            {
                object extension = behaviorExtension.GetType().InvokeMember("CreateBehavior", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Instance, null, behaviorExtension, null);
                yield return ((IEndpointBehavior)extension);
            }
        }
    }
}