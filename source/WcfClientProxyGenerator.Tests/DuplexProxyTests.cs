using System;
using System.ServiceModel;
using System.Threading;
using NSubstitute;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class DuplexProxyTests : TestBase
    {
        [Test]
        public void DuplexService_TriggersCallback()
        {
            var resetEvent = new AutoResetEvent(false);

            var callback = Substitute.For<IDuplexServiceCallback>();
            callback
                .TestCallback(Arg.Any<string>())
                .Returns(m => m.Arg<string>())
                .AndDoes(_ => resetEvent.Set());

            var proxy = GenerateProxy<IDuplexService>(c =>
            {
                c.SetEndpoint(
                    this.TestServer.Binding,
                    this.GetAddress<IDuplexService>(),
                    new InstanceContext(callback));
            });
            
            proxy.Test("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }

        [Test]
        public void DuplexService_OneWayOperation_TriggersCallback()
        {
            var resetEvent = new AutoResetEvent(false);

            var callback = Substitute.For<IDuplexServiceCallback>();

            callback
                .When(m => m.OneWayCallback(Arg.Any<string>()))
                .Do(_ => resetEvent.Set());

            var proxy = GenerateProxy<IDuplexService>(c =>
            {
                c.SetEndpoint(
                    this.TestServer.Binding,
                    GetAddress<IDuplexService>(),
                    new InstanceContext(callback));
            });

            proxy.OneWay("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }

        [Test]
        public void DuplexService_WithInstanceContext_TriggersCallback()
        {
            var resetEvent = new AutoResetEvent(false);

            var callback = Substitute.For<IDuplexServiceCallback>();

            callback
                .TestCallback(Arg.Any<string>())
                .Returns(m => m.Arg<string>())
                .AndDoes(_ => resetEvent.Set());

            InstanceContext<IDuplexServiceCallback> ctx = new InstanceContext<IDuplexServiceCallback>(callback);
            
            var proxy = GenerateProxy<IDuplexService>(c =>
            {
                c.SetEndpoint(
                    this.TestServer.Binding,
                    GetAddress<IDuplexService>(),
                    ctx);
            });

            proxy.Test("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }
    }


}