using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
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
    }
}
