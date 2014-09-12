using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class AsyncProxyTests
    {
        [Test]
        public async Task AsyncProxy_MethodWithReturnValue()
        {
            var mockService = new Mock<ITestService>();
            mockService
                .SetupSequence(m => m.TestMethod("good"))
                .Returns("BAD")
                .Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsync<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.RetryOnResponse<string>(s => s == "BAD");
            });

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            string result = await proxy.CallAsync(m => m.TestMethod("good"));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            Assert.That(result, Is.EqualTo("OK"));
        }
        
        [Test]
        public async Task AsyncProxy_VoidMethod()
        {
            var resetEvent = new AutoResetEvent(false);

            var mockService = new Mock<ITestService>();
            mockService
                .Setup(m => m.VoidMethod("good"))
                .Callback(() =>
                {
                    Console.WriteLine("Callback thread: " + Thread.CurrentThread.ManagedThreadId);
                    resetEvent.Set();
                });

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsync<ITestService>(c => 
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            await proxy.CallAsync(m => m.VoidMethod("good"));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(2)))
                Assert.Fail("Callback never triggered");

        }

//        [Test]
//        public async Task AsyncProxy_VoidMethod()
//        {
//            var mockService = new Mock<ITestService>();
//            mockService
//                .Setup(m => m.VoidMethod("good"))
//                .Callback<string>(input => Assert.That(input, Is.EqualTo("good")));
//
//            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));
//
//            var proxy = WcfClientProxy.CreateAsync<ITestService>(c =>
//                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));
//
//            await proxy.CallAsync(m => m.VoidMethod("good"));
//            mockService.Verify(m => m.VoidMethod("good"));
//        }

        [Test]
        public void AsyncProxy_CanCallIntoSyncProxy()
        {
            var mockService = new Mock<ITestService>();
            mockService
                .Setup(m => m.TestMethod("good"))
                .Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsync<ITestService>(c =>
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            string result = proxy.Client.TestMethod("good");
            Assert.That(result, Is.EqualTo("OK"));
        }
    }
}