using System;
using System.ServiceModel;
using System.Threading;
using Moq;
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

            var callback = new Mock<IDuplexServiceCallback>();
            callback
                .Setup(m => m.TestCallback(It.IsAny<string>()))
                .Callback((string input) =>
                {
                    resetEvent.Set();
                });

            var proxy = WcfClientProxy.Create<IDuplexService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress, callback.Object);
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

            var callback = new Mock<IDuplexServiceCallback>();
            callback
                .Setup(m => m.OneWayCallback(It.IsAny<string>()))
                .Callback((string input) =>
                {
                    resetEvent.Set();
                });

            var proxy = WcfClientProxy.Create<IDuplexService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress, callback.Object);
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

            var callback = new Mock<IDuplexServiceCallback>();
            callback
                .Setup(m => m.TestCallback(It.IsAny<string>()))
                .Callback((string input) =>
                {
                    resetEvent.Set();
                });

            InstanceContext ctx = new InstanceContext(callback);
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