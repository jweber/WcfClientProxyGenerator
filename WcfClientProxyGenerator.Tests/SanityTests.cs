using System;
using System.ComponentModel;
using System.ServiceModel;

using Shouldly;
using WcfClientProxyGenerator.Tests.Infrastructure;
using WcfClientProxyGenerator.Tests.Services;
using Xunit;

namespace WcfClientProxyGenerator.Tests
{
    public class SanityTests : TestBase
    {
        [Fact]
        public void Channel_WorksAsExpected()
        {
            var proxy = new ChannelFactory<ITestService>(this.TestServer.Binding, GetAddress<ITestService>())
                .CreateChannel();

            var response = proxy.Echo("hello world");
        
            response.ShouldBe("hello world");
        }

        [Fact, Description("Asserts that we can fault a default Client Channel")]
        public void FaultHappens_WithDefaultChannelProxy()
        {
            var proxy = new ChannelFactory<ITestService>(this.TestServer.Binding, GetAddress<ITestService>())
                .CreateChannel();

            Should.Throw<Exception>(() => proxy.UnhandledExceptionOnFirstCallThenEcho("hello world"));
            Should.Throw<CommunicationObjectFaultedException>(() => proxy.UnhandledExceptionOnFirstCallThenEcho("hello world"));
            
            var communicationObject = (ICommunicationObject) proxy;
            communicationObject.State.ShouldBe(CommunicationState.Faulted);
        }
    }
}
