using System.ServiceModel;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class ChannelFactoryProviderTests
    {
        [Test]
        public void ChannelFactories_WithIdenticalConfiguration_AreSameInstance_ForCodeBasedConfiguration()
        {
            var factory1 = ChannelFactoryProvider.GetChannelFactory<ITestService>(
                new WSHttpBinding(), 
                new EndpointAddress("http://localhost:23456/TestService"));

            var factory2 = ChannelFactoryProvider.GetChannelFactory<ITestService>(
                new WSHttpBinding(), 
                new EndpointAddress("http://localhost:23456/TestService"));

            Assert.AreSame(factory1, factory2);
        }
        
        [Test]
        public void ChannelFactories_WithNonIdenticalConfiguration_AreNotSameInstance_ForCodeBasedConfiguration()
        {
            var factory1 = ChannelFactoryProvider.GetChannelFactory<ITestService>(
                new WSHttpBinding(), 
                new EndpointAddress("http://localhost:23456/TestService"));

            var factory2 = ChannelFactoryProvider.GetChannelFactory<ITestService>(
                new WSHttpBinding(), 
                new EndpointAddress("http://localhost:23456/TestService2"));

            Assert.AreNotSame(factory1, factory2);
        }

        [Test]
        public void ChannelFactories_WithIdenticalConfiguration_AreSameInstance_ForEndpointConfigurationName()
        {
            var factory1 = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");
            var factory2 = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");

            Assert.AreSame(factory1, factory2);
        }

        [Test]
        public void ChannelFactories_WithNonIdenticalConfiguration_AreNotSameInstance__ForEndpointConfigurationName()
        {
            var factory1 = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");
            var factory2 = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService2");

            Assert.AreNotSame(factory1, factory2);
        }
    }
}
