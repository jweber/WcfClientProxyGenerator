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
    }

    [ServiceContract(CallbackContract = typeof(IDuplexServiceCallback))]
    public interface IDuplexService
    {
        [OperationContract]
        string Test(string input);
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public class DuplexService : IDuplexService
    {
        public string Test(string input)
        {
            var callBackResponse = Callback.TestCallback(input);
            return $"Method Echo: {callBackResponse}";
        }

        IDuplexServiceCallback Callback => OperationContext.Current.GetCallbackChannel<IDuplexServiceCallback>();
    }

    public interface IDuplexServiceCallback
    {
        [OperationContract]
        string TestCallback(string input);
    }

    public class DuplexServiceCallback : IDuplexServiceCallback
    {
        public string TestCallback(string input)
        {
            return $"Callback Echo: {input}";
        }
    }
}