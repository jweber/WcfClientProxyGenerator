using System;
using System.Reflection;
using System.ServiceModel;

using WcfClientProxyGenerator.Async;
using WcfClientProxyGenerator.Tests.Services.Infrastructure;
using Xunit;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public abstract class TestBase : IDisposable
    {
        protected TestServer TestServer { get; private set; }

        public TestBase()
        {
            this.TestServer = TestServer.Start("netTcp");
        }

        public void Dispose()
        {
            TestServer?.Dispose();
        }

        protected EndpointAddress GetAddress<TServiceInterface>()
            where TServiceInterface : class
        {
            var servicePathAttribute = typeof(TServiceInterface).GetCustomAttribute<ServicePathAttribute>();
            if (servicePathAttribute == null)
                throw new NullReferenceException($"The type '{typeof(TServiceInterface).Name}' must have a `ServicePathAttribute`");

            return new EndpointAddress(this.TestServer.BaseAddress + servicePathAttribute.Path.TrimStart('/'));
        }

        protected TServiceInterface GenerateProxy<TServiceInterface>(
            Action<IRetryingProxyConfigurator> config = null)
            where TServiceInterface : class
        {
            return WcfClientProxy.Create<TServiceInterface>(c =>
            {
                c.SetEndpoint(this.TestServer.Binding, GetAddress<TServiceInterface>());
                config?.Invoke(c);
            });
        }
        
        protected IAsyncProxy<TServiceInterface> GenerateAsyncProxy<TServiceInterface>(
            Action<IRetryingProxyConfigurator> config = null)
            where TServiceInterface : class
        {
            return WcfClientProxy.CreateAsyncProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(this.TestServer.Binding, GetAddress<TServiceInterface>());
                config?.Invoke(c);
            });
        }
    }
}
