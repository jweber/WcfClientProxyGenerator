using System;
using System.ServiceModel;
using System.Threading;
using NSubstitute;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class DuplexProxyTests
    {
        [Test]
        public void DuplexService_TriggersCallback()
        {
            var resetEvent = new AutoResetEvent(false);

            var serviceHost = InProcTestFactory.CreateHost<IDuplexService>(new DuplexService());

            var callback = Substitute.For<IDuplexServiceCallback>();

            callback
                .TestCallback(Arg.Any<string>())
                .Returns(m => m.Arg<string>())
                .AndDoes(_ => resetEvent.Set());
            
            var proxy = WcfClientProxy.Create<IDuplexService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress, callback);
            });

            proxy.Test("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }

        [Test]
        public void DuplexService_OneWayOperation_TriggersCallback()
        {
            var resetEvent = new AutoResetEvent(false);

            var serviceHost = InProcTestFactory.CreateHost<IDuplexService>(new DuplexService());

            var callback = Substitute.For<IDuplexServiceCallback>();

            callback
                .When(m => m.OneWayCallback(Arg.Any<string>()))
                .Do(_ => resetEvent.Set());

            var proxy = WcfClientProxy.Create<IDuplexService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress, callback);
            });

            proxy.OneWay("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }

        [Test]
        public void DuplexService_WithInstanceContext_TriggersCallback()
        {
            var resetEvent = new AutoResetEvent(false);

            var serviceHost = InProcTestFactory.CreateHost<IDuplexService>(new DuplexService());

            var callback = Substitute.For<IDuplexServiceCallback>();

            callback
                .TestCallback(Arg.Any<string>())
                .Returns(m => m.Arg<string>())
                .AndDoes(_ => resetEvent.Set());

            InstanceContext<IDuplexServiceCallback> ctx = new InstanceContext<IDuplexServiceCallback>(callback);
            var proxy = WcfClientProxy.Create<IDuplexService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress, ctx);
            });

            proxy.Test("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }
    }

    [ServiceContract(CallbackContract = typeof(IDuplexServiceCallback))]
    public interface IDuplexService
    {
        [OperationContract]
        string Test(string input);

        [OperationContract(IsOneWay = true)]
        void OneWay(string input);
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public class DuplexService : IDuplexService
    {
        public string Test(string input)
        {
            var callBackResponse = Callback.TestCallback(input);
            return $"Method Echo: {callBackResponse}";
        }

        public void OneWay(string input)
        {
            Callback.OneWayCallback(input);
        }

        IDuplexServiceCallback Callback => OperationContext.Current.GetCallbackChannel<IDuplexServiceCallback>();
    }

    public interface IDuplexServiceCallback
    {
        [OperationContract]
        string TestCallback(string input);

        [OperationContract(IsOneWay = true)]
        void OneWayCallback(string input);
    }
}