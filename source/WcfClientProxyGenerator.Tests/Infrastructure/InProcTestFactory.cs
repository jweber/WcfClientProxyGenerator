using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
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

        public static TServiceInterface CreateHostWithClientProxy<TServiceInterface>(object serviceInstance)
            where TServiceInterface : class
        {
            var hostInformation = CreateHost<TServiceInterface>(serviceInstance);
            return ChannelFactory<TServiceInterface>.CreateChannel(hostInformation.Binding, hostInformation.EndpointAddress);
        }

        public static HostInformation CreateHost<TServiceInterface>(object serviceInstance)
            where TServiceInterface : class
        {
            var address = OpenHost<TServiceInterface>(serviceInstance);
            return new HostInformation(Binding, address);
        }

        private static EndpointAddress OpenHost<TServiceInterface>(object serviceInstance)
            where TServiceInterface : class
        {
            var host = new ServiceHost(serviceInstance);
            string address = BaseAddress + Guid.NewGuid();
            host.AddServiceEndpoint(typeof(TServiceInterface), Binding, address);

            host.Description.Behaviors.Find<ServiceBehaviorAttribute>().InstanceContextMode = InstanceContextMode.Single;

            host.Open();

            var endpointAddress = new EndpointAddress(address);
            Hosts[endpointAddress] = host;

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