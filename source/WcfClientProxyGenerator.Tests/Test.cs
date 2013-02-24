using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    public class Test
    {
        [Test]
        public void TestIt()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");
            mockService.Setup(m => m.TestMethod("bad")).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            //var proxy = ProxyGenerator.Create<ITestService>(serviceHost.Binding, serviceHost.EndpointAddress);
            var proxy = ProxyGenerator.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            var result = proxy.TestMethod("good");
            Assert.AreEqual("OK", result);
        }

        [Test]
        public void SanityCheck_Fault_Happens_With_Default_Channel_Proxy()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");
            mockService.Setup(m => m.TestMethod("bad")).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = new ChannelFactory<ITestService>(serviceHost.Binding, serviceHost.EndpointAddress).CreateChannel();

            // Will fault the channel
            Assert.That(() => proxy.TestMethod("bad"), Throws.Exception);
            Assert.That(() => proxy.TestMethod("good"), Throws.Exception.TypeOf<CommunicationObjectFaultedException>());
        }

        [Test]
        public void Test2()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");
            mockService.Setup(m => m.TestMethod("bad")).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = ProxyGenerator.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethod("bad"), Throws.Exception);
            Assert.That(() => proxy.TestMethod("good"), Is.EqualTo("OK"));
        }

        [Test]
        public void Test3()
        {
            var badRequest = new Request() { RequestMessage = "bad" };
            var goodRequest = new Request() { RequestMessage = "good" };

            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethodComplex(goodRequest)).Returns(new Response() { ResponseMessage = "OK" });
            mockService.Setup(m => m.TestMethodComplex(badRequest)).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = ProxyGenerator.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethodComplex(badRequest), Throws.Exception);
            Assert.That(() => proxy.TestMethodComplex(goodRequest).ResponseMessage, Is.EqualTo("OK"));
        }

        [Test]
        public void Test4()
        {
            var badRequest = new Request() { RequestMessage = "bad" };
            var goodRequest = new Request() { RequestMessage = "good" };

            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethodComplexMulti("good", goodRequest)).Returns(new Response() { ResponseMessage = "OK" });
            mockService.Setup(m => m.TestMethodComplexMulti("bad", badRequest)).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = ProxyGenerator.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethodComplexMulti("bad", badRequest), Throws.Exception);
            Assert.That(() => proxy.TestMethodComplexMulti("good", goodRequest).ResponseMessage, Is.EqualTo("OK"));
        }
    }
}
