using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using WcfClientProxyGenerator.Async;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public static class HostingExtensions
    {
        public static HostInformation StartHost<TServiceInterface>(this TServiceInterface serviceInstance)
        {
            var serviceInterface = GetServiceInterfaceType(serviceInstance);
            return InProcTestFactory.CreateHost(serviceInterface, serviceInstance);
        }

        public static TServiceInterface StartHostAndProxy<TServiceInterface>(this TServiceInterface serviceInstance, Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var host = serviceInstance.StartHost();
            return WcfClientProxy.Create<TServiceInterface>(c =>
            {
                c.SetEndpoint(host.Binding, host.EndpointAddress);
                configurator?.Invoke(c);
            });
        }

        public static IAsyncProxy<TServiceInterface> StartHostAndAsyncProxy<TServiceInterface>(this TServiceInterface serviceInstance, Action<IRetryingProxyConfigurator> configurator = null)
            where TServiceInterface : class
        {
            var host = serviceInstance.StartHost();
            return WcfClientProxy.CreateAsyncProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(host.Binding, host.EndpointAddress);
                configurator?.Invoke(c);
            });
        }

        private static Type GetServiceInterfaceType(object serviceInstance)
        {
            var serviceInterfaceType = serviceInstance.GetType()
                .GetInterfaces()
                .FirstOrDefault(m => m.GetCustomAttribute<ServiceContractAttribute>() != null);

            if (serviceInterfaceType == null)
                throw new Exception("Unable to find interface marked with ServiceContractAttribute");

            return serviceInterfaceType;
        }
    }

    public class HostInformation
    {
        public HostInformation(Binding binding, EndpointAddress endpointAddress)
        {
            this.Binding = binding;
            this.EndpointAddress = endpointAddress;
        }

        public Binding Binding { get; private set; }
        public EndpointAddress EndpointAddress { get; private set; }
    }

    public static class InProcTestFactory
    {
        private static readonly string BaseAddress = "net.pipe://localhost/" + Guid.NewGuid();
        private static readonly Binding Binding;

        private static readonly IDictionary<EndpointAddress, ServiceHost> Hosts
            = new ConcurrentDictionary<EndpointAddress, ServiceHost>();

        static InProcTestFactory()
        {
            var binding = new NetNamedPipeBinding();
            binding.TransactionFlow = true;

            Binding = binding;
        }

        public static HostInformation CreateHost<TServiceInterface>(object serviceInstance)
            where TServiceInterface : class
            => CreateHost(typeof(TServiceInterface), serviceInstance);

        public static HostInformation CreateHost(Type serviceInterfaceType, object serviceInstance)
        {
            var address = OpenHost(serviceInterfaceType, serviceInstance);
            return new HostInformation(Binding, address);
        }

        private static EndpointAddress OpenHost(Type serviceInterfaceType, object serviceInstance)
        {
            var host = new ServiceHost(serviceInstance);
            string address = BaseAddress + Guid.NewGuid();
            host.AddServiceEndpoint(serviceInterfaceType, Binding, address);

            host.Description.Behaviors.Find<ServiceBehaviorAttribute>().InstanceContextMode = InstanceContextMode.Single;

            host.Open();

            var endpointAddress = new EndpointAddress(address);
            Hosts[endpointAddress] = host;

            foreach (var endpoint in host.Description.Endpoints)
            {
                foreach (var operation in endpoint.Contract.Operations)
                {
                    string operationName = operation.Name;
                    string action = operation.Messages[0].Action;
                    if (operation.Messages.Count > 1)
                    {
                        string replyAction = operation.Messages[1].Action;
                    }
                }
            }

            return endpointAddress;
        }
        
        public static void CloseHost(EndpointAddress endpointAddress)
        {
            if (!Hosts.ContainsKey(endpointAddress))
                return;

            Hosts[endpointAddress].Close();
            Hosts.Remove(endpointAddress);
        }

        public static void CloseHosts()
        {
            foreach (var address in Hosts.Keys)
                CloseHost(address);
        }
    }
}