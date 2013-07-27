using System;
using System.ServiceModel;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Policy;
using WcfClientProxyGenerator.Tests.Infrastructure;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class ProxyTests
    {
        [Test] 
        [Description("Asserts that when no conguration is given in the Create proxy call, the endpoint config that matches the contract will be used")]
        public void CreatingProxy_WithNoConfigurator_AndSingleEndpointConfig_GetsDefaultClientConfiguration()
        {
            WcfClientProxy.Create<ITestServiceSingleEndpointConfig>();
        }

        [Test] 
        [Description("Asserts that when no conguration is given in the Create proxy call, and multiple endpoint configs for the contract exist, an exception is thrown")]
        public void CreatingProxy_WithNoConfigurator_AndMultipleEndpointConfigs_ThrowsException()
        {
            Assert.That(
                () => WcfClientProxy.Create<ITestService>(),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CreatingProxy_WithNoConfigurator_AndNoDefaultConfiguration_ThrowsException()
        {
            Assert.That(
                () => WcfClientProxy.Create<ITestService2>(), 
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CreatingProxy_WithEndpointConfigurationName_ThatExists_CreatesProxy()
        {
            WcfClientProxy.Create<ITestService>("ITestService2");
        }

        [Test]
        public void CreatingProxy_WithEndpointConfigurationName_ThatDoesNotExist_ThrowsException()
        {
            Assert.That(
                () => WcfClientProxy.Create<ITestService>("DoesNotExist"),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Proxy_ReturnsExpectedValue_WhenCallingService()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));
            
            var result = proxy.TestMethod("good");
            Assert.AreEqual("OK", result);
        }

        [Test]
        public void Proxy_CanCallVoidMethod()
        {
            var mockService = new Mock<ITestService>();
            mockService
                .Setup(m => m.VoidMethod("good"))
                .Callback<string>(input =>
                {
                    Assert.That(input, Is.EqualTo("good"));
                });

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));
            proxy.VoidMethod("good");
        }

        [Test]
        public void MultipleProxies_ReturnExpectedValues_WhenCallingServices()
        {
            var mockService1 = new Mock<ITestService>();
            mockService1.Setup(m => m.TestMethod("service1")).Returns("OK from service 1");

            var mockService2 = new Mock<ITestService2>();
            mockService2.Setup(m => m.TestMethod("service2")).Returns("OK from service 2");

            var serviceHost1 = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService1));
            var serviceHost2 = InProcTestFactory.CreateHost<ITestService2>(new TestService2Impl(mockService2));

            var proxy1 = WcfClientProxy.Create<ITestService>(
                    c => c.SetEndpoint(serviceHost1.Binding, serviceHost1.EndpointAddress));

            var proxy2 = WcfClientProxy.Create<ITestService2>(
                    c => c.SetEndpoint(serviceHost2.Binding, serviceHost2.EndpointAddress));

            Assert.AreEqual("OK from service 1", proxy1.TestMethod("service1"));
            Assert.AreEqual("OK from service 2", proxy2.TestMethod("service2"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingSimpleMethod()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");
            mockService.Setup(m => m.TestMethod("bad")).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethod("bad"), Throws.Exception);
            Assert.That(() => proxy.TestMethod("good"), Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingComplexTypeMethod()
        {
            var badRequest = new Request() { RequestMessage = "bad" };
            var goodRequest = new Request() { RequestMessage = "good" };

            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethodComplex(goodRequest)).Returns(new Response() { ResponseMessage = "OK" });
            mockService.Setup(m => m.TestMethodComplex(badRequest)).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethodComplex(badRequest), Throws.Exception);
            Assert.That(() => proxy.TestMethodComplex(goodRequest).ResponseMessage, Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingMultipleParameterComplexTypeMethod()
        {
            var badRequest = new Request() { RequestMessage = "bad" };
            var goodRequest = new Request() { RequestMessage = "good" };

            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethodComplexMulti("good", goodRequest)).Returns(new Response() { ResponseMessage = "OK" });
            mockService.Setup(m => m.TestMethodComplexMulti("bad", badRequest)).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethodComplexMulti("bad", badRequest), Throws.Exception);
            Assert.That(() => proxy.TestMethodComplexMulti("good", goodRequest).ResponseMessage, Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_CanBeGeneratedForInheritingServiceInterface()
        {
            var mockTestService = new Mock<ITestService>();
            var mockChildService = new Mock<IChildService>();

            mockChildService
                .Setup(m => m.ChildMethod(It.IsAny<string>()))
                .Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<IChildService>(new ChildServiceImpl(mockTestService, mockChildService));

            var proxy = WcfClientProxy.Create<IChildService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            Assert.That(() => proxy.ChildMethod("test"), Is.EqualTo("OK"));
        }
    }
}
