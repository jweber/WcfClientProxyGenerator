using System;
using System.Reflection;
using System.ServiceModel;
using NUnit.Framework;
using WcfClientProxyGenerator.Standard.Async;
using WcfClientProxyGenerator.Tests.Services.Infrastructure;

namespace WcfClientProxyGenerator.Standard.Tests.Infrastructure
{
    [SetUpFixture]
    public abstract class TestBase
    {
        protected TestServer TestServer { get; private set; }

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
        
        [SetUp]
        public void Setup()
        {
            this.TestServer = TestServer.Start("netTcp");
        }

        [TearDown]
        public void TearDown()
        {
            this.TestServer.Dispose();
        }
    }
}
