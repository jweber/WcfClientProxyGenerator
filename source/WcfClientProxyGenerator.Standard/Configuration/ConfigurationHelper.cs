#if NETFULL

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;

namespace WcfClientProxyGenerator.Standard.Configuration
{
    internal class ConfigurationHelper
    {
        public static ServiceEndpoint GetClientEndpointConfiguration<TServiceInterface>(
            Type serviceInterfaceType,
            string endpointConfigurationName = null)
            where TServiceInterface : class
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

            var serviceEndpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof(TServiceInterface)))
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
                yield return ((IEndpointBehavior) extension);
            }
        }
    }
}

#endif