using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public class TestServer : IDisposable
    {
        private readonly Process serverProcess;
        
        private TestServer(string executablePath, EndpointAddress baseAddress, string bindingType = "http")
        {
            this.BaseAddress = baseAddress;
            this.Binding = GetBindingType(bindingType).binding;
            
            this.serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = string.Join(" ", baseAddress.Uri.Port.ToString(), bindingType),
//                    RedirectStandardOutput = true,
//                    RedirectStandardError = true,
                    ErrorDialog = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            this.serverProcess.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            this.serverProcess.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
            
            this.serverProcess.Start();
        }

        public EndpointAddress BaseAddress { get; private set; }
        public Binding Binding { get; private set; }

        public static TestServer Start(string bindingType = "http")
        {
            var serviceHostPath = WcfServiceHostPath();
            
            int port = FindFreePort();
            var address = new EndpointAddress($"{GetBindingType(bindingType).protocol}://localhost:{port}");
            
            return new TestServer(serviceHostPath, address, bindingType);
        }

        private static string WcfServiceHostPath()
        {
            string[] SearchResults(string path)
                => Directory.GetFiles(path, "WcfClientProxyGenerator.Tests.WcfServiceHost.exe", SearchOption.AllDirectories);

            var paths = new[] { ".", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "WcfServiceHost") };
            foreach (var path in paths)
            {
                var files = SearchResults(path);
                if (files != null && files.Any())
                    return files[0];
            }
            
            throw new FileNotFoundException($"Could not locate WcfClientProxyGenerator.Tests.WcfServiceHost.exe under {string.Join("; ", paths)}");
        }

        private static (string protocol, Binding binding) GetBindingType(string bindingType)
        {
            bindingType = bindingType.ToLowerInvariant();
            
            switch (bindingType)
            {
                case "http":
                    return ("http", new BasicHttpBinding());
                case "nettcp":
                case "tcp":
                    return ("net.tcp", new NetTcpBinding());
                default:
                    throw new ArgumentException($"Could not map '{bindingType}' to an actual binding type");
            }
        }

        public void Dispose()
        {
            serverProcess?.Kill();
        }

        private static int FindFreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint) l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}