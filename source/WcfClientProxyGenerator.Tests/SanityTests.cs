using System;
using System.ServiceModel;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    public class SanityTests : TestBase
    {
        [Test, Description("Asserts that we can mock a WCF service in memory")]
        public void MockedService_WorksAsExpected()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("known")
                .Returns("test");

            var host = service.StartHost();

            var proxy = ChannelFactory<ITestService>.CreateChannel(host.Binding, host.EndpointAddress);

            Assert.That(() => proxy.TestMethod("known"), Is.EqualTo("test"));
        }

        [Test, Description("Asserts that we can fault a default Client Channel")]
        public void FaultHappens_WithDefaultChannelProxy()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("good")
                .Returns("OK");

            service
                .TestMethod("bad")
                .Throws<Exception>();

            var host = service.StartHost<ITestService>();

            var proxy = new ChannelFactory<ITestService>(host.Binding, host.EndpointAddress).CreateChannel();

            // Will fault the channel
            Assert.That(() => proxy.TestMethod("bad"), Throws.Exception);
            Assert.That(() => proxy.TestMethod("good"), Throws.Exception.TypeOf<CommunicationObjectFaultedException>());
        }
    }
}
