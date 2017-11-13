using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace WcfClientProxyGenerator.Standard.Tests
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
            var basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "WcfServiceHost");
            var files = Directory.GetFiles(basePath, "WcfClientProxyGenerator.Tests.WcfServiceHost.exe", SearchOption.AllDirectories);
            
            if (files == null || !files.Any())
                throw new FileNotFoundException($"Could not locate WcfClientProxyGenerator.Tests.WcfServiceHost.exe under {basePath}");
            
            int port = FindFreePort();
            var address = new EndpointAddress($"{GetBindingType(bindingType).protocol}://localhost:{port}");
            
            return new TestServer(files[0], address, bindingType);
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
        
        public EndpointAddress Path(string path) 
            => new EndpointAddress(this.BaseAddress.ToString() + path);

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