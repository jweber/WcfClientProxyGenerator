using System;
using System.ServiceModel;
using System.Threading;
using NUnit.Framework;
using WcfClientProxyGenerator.Standard.Async;

namespace WcfClientProxyGenerator.Standard.Tests
{
    [SetUpFixture]
    public abstract class TestBase
    {
        protected TestServer TestServer { get; private set; }

        protected TServiceInterface GenerateProxy<TServiceInterface>(
            string path,
            Action<IRetryingProxyConfigurator> config = null)
            where TServiceInterface : class
        {
            return WcfClientProxy.Create<TServiceInterface>(c =>
            {
                c.SetEndpoint(this.TestServer.Binding, new EndpointAddress(this.TestServer.BaseAddress + path));
                config?.Invoke(c);
            });
        }
        
        protected IAsyncProxy<TServiceInterface> GenerateAsyncProxy<TServiceInterface>(
            string path,
            Action<IRetryingProxyConfigurator> config = null)
            where TServiceInterface : class
        {
            return WcfClientProxy.CreateAsyncProxy<TServiceInterface>(c =>
            {
                c.SetEndpoint(this.TestServer.Binding, new EndpointAddress(this.TestServer.BaseAddress + path));
                config?.Invoke(c);
            });
        }
        
        [SetUp]
        public void Setup()
        {
            this.TestServer = TestServer.Start();
        }

        [TearDown]
        public void TearDown()
        {
            this.TestServer.Dispose();
        }
    }
}
