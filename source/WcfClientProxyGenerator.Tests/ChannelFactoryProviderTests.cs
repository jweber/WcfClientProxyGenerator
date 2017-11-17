using System;
using System.ComponentModel;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using Shouldly;
using WcfClientProxyGenerator.Tests.Services;
using Xunit;

namespace WcfClientProxyGenerator.Tests
{
    [ServiceContract]
    public interface INoConfigService
    {
        [OperationContract]
        string Echo(string input);
    }
    
    public class ChannelFactoryProviderTests
    {
        [Fact]
        public void ChannelFactories_WithIdenticalConfiguration_AreSameInstance_ForCodeBasedConfiguration()
        {
            var factory1 = ChannelFactoryProvider.GetChannelFactory<ITestService>(
                new NetTcpBinding(), 
                new EndpointAddress("http://localhost:23456/TestService"));

            var factory2 = ChannelFactoryProvider.GetChannelFactory<ITestService>(
                new NetTcpBinding(), 
                new EndpointAddress("http://localhost:23456/TestService"));

            factory1.ShouldBeSameAs(factory2);
        }
        
        [Fact]
        public void ChannelFactories_WithNonIdenticalConfiguration_AreNotSameInstance_ForCodeBasedConfiguration()
        {
            var factory1 = ChannelFactoryProvider.GetChannelFactory<ITestService>(
                new NetTcpBinding(), 
                new EndpointAddress("http://localhost:23456/TestService"));

            var factory2 = ChannelFactoryProvider.GetChannelFactory<ITestService>(
                new NetTcpBinding(), 
                new EndpointAddress("http://localhost:23456/TestService2"));

            factory1.ShouldNotBeSameAs(factory2);
        }
        
#if NETFULL

        /// <summary>
        /// Issue #19 exposed a failure where using the endpointConfigurationName
        /// to generate a proxy would not use the dynamically generated *Async service
        /// interface to create the channel. This would cause a NotSupportedException
        /// to be thrown when attempting to make a service call using a generated *Async
        /// method.
        /// </summary>
        [Fact, Description("Github issue #19")]
        public async Task UsingEndpointConfigurationName_BuildServiceEndpointForChannelFactory_UsingDynamicallyGeneratedAsyncInterface()
        {
            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>("ITestService");

            var exception = await Assert.ThrowsAsync<EndpointNotFoundException>(
                () => proxy.CallAsync(m => m.Echo("test")));

            exception.Message.ShouldStartWith("There was no endpoint listening at ");
        }

        [Fact]
        public void ChannelFactory_FromEndpointConfigurationName_WithBehaviorConfiguration_ContainsConfiguredBehaviors()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("BehaviorService");
            var behavior = factory.Endpoint.Behaviors.Find<WebHttpBehavior>();

            behavior.ShouldNotBeNull();
            behavior.HelpEnabled.ShouldBeTrue();
        }

        [Fact]
        public void ChannelFactory_FromEndpointConfigurationName_WithoutBehaviorConfiguratio_DoesNotContainEndpointBehaviors()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");
            var behavior = factory.Endpoint.Behaviors.Find<WebHttpBehavior>();

            behavior.ShouldBeNull();
        }

        [Fact]
        public void ChannelFactory_WithSingleConfigurationForContract_UsesDefaultConfiguration()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestServiceSingleEndpointConfig>();
            
            factory.Endpoint.Address.ToString().ShouldBe("http://localhost:23456/TestService2");
            factory.Endpoint.Binding.Name.ShouldBe("WSHttpBinding");
        }
    
        [Fact]
        public void ChannelFactory_ClientEndpoint_WithCustomBindingConfiguration()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");

            
            factory.Endpoint.Address.ToString().ShouldBe("http://localhost:23456/TestService");
            
            var binding = (WSHttpBinding) factory.Endpoint.Binding;
            binding.Name.ShouldBe("wsHttpBinding_ITestService");
            binding.MaxReceivedMessageSize.ShouldBe(12345);
        }

        [Fact]
        public void NoConfigurationForServiceType_ThrowsInvalidOperationException()
        {
            Should.Throw<InvalidOperationException>(() => ChannelFactoryProvider.GetChannelFactory<INoConfigService>());
        }

        [Fact]
        public void ChannelFactories_WithIdenticalConfiguration_AreSameInstance_ForEndpointConfigurationName()
        {
            var factory1 = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");
            var factory2 = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");

            factory1.ShouldBeSameAs(factory2);
        }

        [Fact]
        public void ChannelFactories_WithNonIdenticalConfiguration_AreNotSameInstance__ForEndpointConfigurationName()
        {
            var factory1 = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");
            var factory2 = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService2");

            factory1.ShouldNotBeSameAs(factory2);
        }

        [Fact]
        public void ServiceInterface_WithMultipleClientEndpoints_ThrowsInvalidOperationException_WhenUsingDefaultCtor()
        {
            Should.Throw<InvalidOperationException>(() => ChannelFactoryProvider.GetChannelFactory<ITestService>());
        }
    
#endif
        
    }
}
