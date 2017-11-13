using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Tests.WcfServiceHost
{
    enum BindingType
    {
        Http,
        NetTcp
    }

    class Program
    {
        private static readonly IList<ServiceHost> Hosts = new List<ServiceHost>();

        static void Main(string[] args)
        {
            if (args == null || !args.Any())
                throw new NullReferenceException("The port to bind the services on must be passed as the first argument");

            var binding = args.Length > 1 ? GetBindingType(args[1]) : GetBindingType("http");

            var baseAddress = $"{binding.protocol}://localhost:{args[0]}";

            Boot<ITestService, TestService>(baseAddress, "/test", binding.binding);
            Boot<IAsyncService, AsyncService>(baseAddress, "/async", binding.binding);
            Boot<IOutParamTestService, OutParamsTestService>(baseAddress, "/out", binding.binding);

            Console.WriteLine("Press <Enter> to stop the service.");
            Console.ReadLine();

            foreach (var host in Hosts)
                host.Close();
        }

        private static void Boot<TService, TServiceImpl>(string baseAddress, string path, Binding binding)
            where TServiceImpl : TService
        {
            var address = new Uri(baseAddress + path);
            var host = new ServiceHost(typeof(TServiceImpl));

            if (binding.GetType() == typeof(BasicHttpBinding))
            {
                var smb = new ServiceMetadataBehavior
                {
                    HttpGetEnabled = true,
                    MetadataExporter = { PolicyVersion = PolicyVersion.Policy15 },
                    HttpGetUrl = address
                };
                host.Description.Behaviors.Add(smb);
            }

            host.AddServiceEndpoint(typeof(TService), binding, address);

            host.Description.Behaviors.Find<ServiceBehaviorAttribute>().InstanceContextMode = InstanceContextMode.Single;
            host.Description.Behaviors.Find<ServiceBehaviorAttribute>().IncludeExceptionDetailInFaults = true;

            host.Open();

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

            Console.WriteLine($"The {typeof(TService).Name} service is ready at {address}");

            Hosts.Add(host);
        }

        private static (string protocol, Binding binding) GetBindingType(string bindingType)
        {
            bindingType = bindingType.ToLowerInvariant();

            switch (bindingType)
            {
                case "http":
                    return ("http", new BasicHttpBinding());
                case "netTcp":
                case "tcp":
                    return ("net.tcp", new NetTcpBinding());
                default:
                    throw new ArgumentException($"Could not map '{bindingType}' to an actual binding type");
            }
        }
    }
}