using System;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using NUnit.Framework;
using WcfClientProxyGenerator.Standard.Tests.Infrastructure;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Standard.Tests
{
    [TestFixture]
    public class SanityTests : TestBase
    {
        [Test]
        public void Channel_WorksAsExpected()
        {
            var proxy = new ChannelFactory<ITestService>(this.TestServer.Binding, GetAddress<ITestService>())
                .CreateChannel();

            var response = proxy.Echo("hello world");
        
            Assert.That(response, Is.EqualTo("hello world"));
        }

        [Test, Description("Asserts that we can fault a default Client Channel")]
        public void FaultHappens_WithDefaultChannelProxy()
        {
            var proxy = new ChannelFactory<ITestService>(this.TestServer.Binding, GetAddress<ITestService>())
                .CreateChannel();

            Assert.That(() => proxy.UnhandledExceptionOnFirstCallThenEcho("hello world"), Throws.Exception);
            Assert.That(() => proxy.UnhandledExceptionOnFirstCallThenEcho("hello world"), Throws.Exception.TypeOf<CommunicationObjectFaultedException>());
            
            var communicationObject = (ICommunicationObject) proxy;
            Assert.That(communicationObject.State, Is.EqualTo(CommunicationState.Faulted));
        }
    }
}
