using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class AsyncTests
    {
        [Test]
        public async Task ServiceContractDefinedAsyncMethod_WithReturnValue()
        {
            var resetEvent = new AutoResetEvent(false);

            var mockService = new Mock<IAsyncTestInterface>();
            mockService
                .Setup(m => m.ReturnMethodDefinedAsync("test"))
                .Returns(Task.FromResult("response"))
                .Callback(() =>
                {
                    Console.WriteLine("Callback thread: " + Thread.CurrentThread.ManagedThreadId);
                    resetEvent.Set();                    
                });

            var serviceHost = InProcTestFactory.CreateHost<IAsyncTestInterface>(new AsyncTestInterfaceImpl(mockService));

            var proxy = WcfClientProxy.Create<IAsyncTestInterface>(c =>
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            string result = await proxy.ReturnMethodDefinedAsync("test");

            Console.WriteLine("Contination thread: " + Thread.CurrentThread.ManagedThreadId);

            Assert.That(result, Is.EqualTo("response"));

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(2)))
                Assert.Fail("Callback never called");
        }
        
        [Test]
        public async Task ServiceContractDefinedAsyncMethod_WithNoReturnValue()
        {
            var resetEvent = new AutoResetEvent(false);

            var mockService = new Mock<IAsyncTestInterface>();
            mockService
                .Setup(m => m.VoidMethodDefinedAsync("test"))
                .Returns(Task.FromResult(true))
                .Callback(() =>
                {
                    Console.WriteLine("Callback thread: " + Thread.CurrentThread.ManagedThreadId);
                    resetEvent.Set();            
                });

            var serviceHost = InProcTestFactory.CreateHost<IAsyncTestInterface>(new AsyncTestInterfaceImpl(mockService));

            var proxy = WcfClientProxy.Create<IAsyncTestInterface>(c =>
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            await proxy.VoidMethodDefinedAsync("test");

            Console.WriteLine("Contination thread: " + Thread.CurrentThread.ManagedThreadId);

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(2)))
                Assert.Fail("Callback never called");
        }

        [Test]
        public async Task CallAsync_MethodWithReturnValue()
        {
            var mockService = new Mock<ITestService>();
            mockService
                .SetupSequence(m => m.TestMethod("good"))
                .Returns("BAD")
                .Returns("OK");

            mockService.Setup(m => m.TestMethod("second", "two")).Returns("2");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>(c =>
            {
                c.MaximumRetries(1);
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.RetryOnResponse<string>(s => s == "BAD");
            });

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            string result = await proxy.CallAsync(m => m.TestMethod("good"));
            string result2 = await proxy.CallAsync(m => m.TestMethod("second", "two"));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            Assert.That(result, Is.EqualTo("OK"));
            Assert.That(result2, Is.EqualTo("2"));
        }

        [Test]
        public async Task CallAsync_MethodWithReturnValue2()
        {
            var request = new Request() { RequestMessage = "test" };

            var mockService = new Mock<ITestService>();
            mockService
                .SetupSequence(m => m.TestMethodComplex(It.IsAny<Request>()))
                .Returns(new Response { ResponseMessage = "test", StatusCode = 1 })
                .Returns(new Response { ResponseMessage  = "test", StatusCode = 0 });

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>(c =>
            {
                c.MaximumRetries(1);
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.RetryOnResponse<Response>(s => s.StatusCode == 1);
            });

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            var result = await proxy.CallAsync(m => m.TestMethodComplex(request));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            Assert.That(result.StatusCode, Is.EqualTo(0));
            Assert.That(result.ResponseMessage, Is.EqualTo("test"));
        }
        
        [Test]
        public async Task CallAsync_VoidMethod()
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

            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>(c => 
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            await proxy.CallAsync(m => m.VoidMethod("good"));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(2)))
                Assert.Fail("Callback never triggered");
        }

        [Test]
        public async Task CallAsync_OneWayOperation()
        {
            var resetEvent = new AutoResetEvent(false);

            var mockService = new Mock<ITestService>();

            mockService
                .Setup(m => m.OneWay(It.IsAny<string>()))
                .Callback((string input) =>
                {
                    Assert.That(input, Is.EqualTo("test"));

                    Console.WriteLine("Callback thread: " + Thread.CurrentThread.ManagedThreadId);
                    resetEvent.Set();
                });

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>(c => 
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            await proxy.CallAsync(m => m.OneWay("test"));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }

        [Test]
        public void CallAsync_MultipleConcurrentCalls()
        {
            int iterations = 20;

            var mockService = new Mock<ITestService2>();
            mockService
                .Setup(m => m.TestMethod(It.IsAny<string>()))
                .Returns((string s) => "Echo: " + s)
                .Callback((string s) => Console.WriteLine("Callback: " + s));

            var serviceHost = InProcTestFactory.CreateHost<ITestService2>(new TestService2Impl(mockService));

            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService2>(c =>
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            var tasks = new List<Task>();
            for (int i = 0; i < iterations; i++)
            {
                int i1 = i;
                tasks.Add(proxy.CallAsync(m => m.TestMethod(i1.ToString())));
                Console.WriteLine("Queued task: " + i);
            }

            Console.WriteLine("Waiting tasks...");

            Task.WaitAll(tasks.ToArray());

            for (int i = 0; i < iterations; i++)
            {
                string result = ((Task<string>) tasks[i]).Result;
                Assert.That(result, Is.EqualTo("Echo: " + i));
            }
        }

        [Test]
        public void CallAsync_CanCallIntoSyncProxy()
        {
            var mockService = new Mock<ITestService>();
            mockService
                .Setup(m => m.TestMethod("good"))
                .Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>(c =>
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            string result = proxy.Client.TestMethod("good");
            Assert.That(result, Is.EqualTo("OK"));
        }

        [Test]
        public void CallAsync_CallingMethodWithByRefParams_ThrowsNotSupportedException()
        {
            var mockService = new Mock<IOutParamTestService>();

            byte[] expectedOutParam = { 0x01 };
            mockService
                .Setup(m => m.SingleOutParam(out expectedOutParam))
                .Returns(100);

            var serviceHost = InProcTestFactory.CreateHost<IOutParamTestService>(new OutParamTestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsyncProxy<IOutParamTestService>(c =>
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            byte[] resultingOutParam;

            Assert.That(() => proxy.CallAsync(m => m.SingleOutParam(out resultingOutParam)), Throws.TypeOf<NotSupportedException>());
        }
    }
}