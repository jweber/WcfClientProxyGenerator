using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    public class SanityTests : TestBase
    {
        [Test, Description("Asserts that we can mock a WCF service in memory")]
        public void MockedService_WorksAsExpected()
        {
            var mockService = new Mock<ITestService>();
            mockService
                .Setup(m => m.TestMethod("known"))
                .Returns("test");

            var proxy = InProcTestFactory.CreateHostWithClientProxy<ITestService>(new TestServiceImpl(mockService));

            Assert.That(() => proxy.TestMethod("known"), Is.EqualTo("test"));
        }

        [Test, Description("Asserts that we can fault a default Client Channel")]
        public void FaultHappens_WithDefaultChannelProxy()
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
        public async Task AsyncWrapperTest()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");
            mockService.Setup(m => m.VoidMethod("test")).Verifiable();
            mockService.Setup(m => m.TestMethodComplex(new Request { RequestMessage = "test" }))
                .Returns(new Response() { ResponseMessage = "OK", StatusCode = 0 });

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = new ChannelFactory<ITestService>(serviceHost.Binding, serviceHost.EndpointAddress).CreateChannel();

            var async = WcfClientProxy.CreateAsync<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));
           
            var response = await async.CallAsync(m => m.TestMethod("good"));
            Assert.That(response, Is.EqualTo("OK"));

            await async.CallAsync(m => m.VoidMethod("test"));
            mockService.Verify(m => m.VoidMethod("test"));

            var complexResponse = await async.CallAsync(m => m.TestMethodComplex(new Request { RequestMessage = "test" }));
            Assert.That(complexResponse.ResponseMessage, Is.EqualTo("OK"));

            // Will fault the channel
//            Assert.That(() => proxy.TestMethod("bad"), Throws.Exception);
//            Assert.That(() => proxy.TestMethod("good"), Throws.Exception.TypeOf<CommunicationObjectFaultedException>());            
        }
    }

}
