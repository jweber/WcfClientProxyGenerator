using System;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Standard.Tests
{
    [TestFixture]
    public class SanityTests : TestBase
    {
        [Test]
        public void Channel_WorksAsExpected()
        {
            var proxy = new ChannelFactory<ITestService>(this.TestServer.Binding, this.TestServer.Path("/test"))
                .CreateChannel();

            var response = proxy.Echo("hello world");
        
            Assert.That(response, Is.EqualTo("hello world"));
        }

        [Test, Description("Asserts that we can fault a default Client Channel")]
        [Ignore("Figure out how to accurately fault a channel")]
        public void FaultHappens_WithDefaultChannelProxy()
        {
            var proxy = new ChannelFactory<ITestService>(this.TestServer.Binding, this.TestServer.Path("/test"))
                .CreateChannel();

            Assert.That(() => proxy.UnhandledException(), Throws.Exception);
            Assert.That(() => proxy.Echo("hello world"), Throws.Exception.TypeOf<CommunicationObjectFaultedException>());
        }
    }
}
