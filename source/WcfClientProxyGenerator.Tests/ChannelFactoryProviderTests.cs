using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class ChannelFactoryProviderTests
    {
        /// <summary>
        /// Issue #19 exposed a failure where using the endpointConfigurationName
        /// to generate a proxy would not use the dynamically generated *Async service
        /// interface to create the channel. This would cause a NotSupportedException
        /// to be thrown when attempting to make a service call using a generated *Async
        /// method.
        /// </summary>
        [Test, Description("Github issue #19")]
        public void UsingEndpointConfigurationName_BuildServiceEndpointForChannelFactory_UsingDynamicallyGeneratedAsyncInterface()
        {
            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>("ITestService");

            var exception = Assert.ThrowsAsync<EndpointNotFoundException>(
                () => proxy.CallAsync(m => m.IntMethod()),
                message: "Expected EndpointNotFoundException (since ITestService address is not running)");

            Assert.That(exception.Message, Does.StartWith("There was no endpoint listening at "));
        }

        [Test]
        public void ChannelFactory_FromEndpointConfigurationName_WithBehaviorConfiguration_ContainsConfiguredBehaviors()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("BehaviorService");
            var behavior = factory.Endpoint.Behaviors.Find<WebHttpBehavior>();
            
            Assert.That(behavior, Is.Not.Null);
            Assert.That(behavior.HelpEnabled, Is.True);
        }

        [Test]
        public void ChannelFactory_FromEndpointConfigurationName_WithoutBehaviorConfiguratio_DoesNotContainEndpointBehaviors()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");
            var behavior = factory.Endpoint.Behaviors.Find<WebHttpBehavior>();

            Assert.That(behavior, Is.Null);
        }

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

        [Test]
        public void ChannelFactory_WithSingleConfigurationForContract_UsesDefaultConfiguration()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestServiceSingleEndpointConfig>();
            
            Assert.That(factory.Endpoint.Address.ToString(), Is.EqualTo("http://localhost:23456/TestService2"));
            Assert.That(factory.Endpoint.Binding.Name, Is.EqualTo("WSHttpBinding"));
        }

        [Test]
        public void ServiceInterface_WithMultipleClientEndpoints_ThrowsInvalidOperationException_WhenUsingDefaultCtor()
        {
            Assert.That(() => ChannelFactoryProvider.GetChannelFactory<ITestService>(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ChannelFactory_ClientEndpoint_WithCustomBindingConfiguration()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("ITestService");

            Assert.That(factory.Endpoint.Address.ToString(), Is.EqualTo("http://localhost:23456/TestService"));
            
            var binding = (WSHttpBinding) factory.Endpoint.Binding;
            Assert.That(binding.Name, Is.EqualTo("wsHttpBinding_ITestService"));
            Assert.That(binding.MaxReceivedMessageSize, Is.EqualTo(12345));
        }

        [Test]
        public void ChannelFactory_Identity_WithSpnIdentity()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("TestService_SpnIdentity");

            Assert.That(factory.Endpoint.Address.Identity, Is.TypeOf<SpnEndpointIdentity>());

            var spnValue = factory.Endpoint.Address.Identity.IdentityClaim;
            Assert.That(spnValue.Resource, Is.EqualTo("Service/host:40000"));
        }

        [Test]
        public void ChannelFactory_Identity_WithDnsIdentity()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("TestService_DnsIdentity");

            Assert.That(factory.Endpoint.Address.Identity, Is.TypeOf<DnsEndpointIdentity>());

            var dnsValue = factory.Endpoint.Address.Identity.IdentityClaim;
            Assert.That(dnsValue.Resource, Is.EqualTo("server"));
        }

        [Test]
        public void ChannelFactory_Identity_WithUpnIdentity()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("TestService_UpnIdentity");

            Assert.That(factory.Endpoint.Address.Identity, Is.TypeOf<UpnEndpointIdentity>());

            var dnsValue = factory.Endpoint.Address.Identity.IdentityClaim;
            Assert.That(dnsValue.Resource, Is.EqualTo("someone@cohowinery.com"));
        }

        [Test]
        public void ChannelFactory_Identity_WithCertificateIdentity()
        {
            var factory = ChannelFactoryProvider.GetChannelFactory<ITestService>("TestService_CertificateIdentity");

            Assert.That(factory.Endpoint.Address.Identity, Is.TypeOf<X509CertificateEndpointIdentity>());

            var certificate = ((X509CertificateEndpointIdentity)factory.Endpoint.Address.Identity).Certificates[0];
            Assert.That(certificate.SubjectName.Name, Is.EqualTo("CN=NVIDIA GameStream Server"));
        }

        [Test]
        public void NoConfigurationForServiceType_ThrowsInvalidOperationException()
        {
            Assert.That(() => ChannelFactoryProvider.GetChannelFactory<IAsyncTestInterface>(), Throws.TypeOf<InvalidOperationException>());
        }
    }
}
