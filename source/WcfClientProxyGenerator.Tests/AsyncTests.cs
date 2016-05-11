using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
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

            var service = Substitute.For<IAsyncTestInterface>();

            service
                .ReturnMethodDefinedAsync("test")
                .Returns(Task.FromResult("response"))
                .AndDoes(m =>
                {
                    Console.WriteLine("Callback thread: " + Thread.CurrentThread.ManagedThreadId);
                    resetEvent.Set();
                });


            var proxy = service.StartHostAndProxy();

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

            var service = Substitute.For<IAsyncTestInterface>();

            service
                .VoidMethodDefinedAsync("test")
                .Returns(Task.FromResult(true))
                .AndDoes(m =>
                {
                    Console.WriteLine("Callback thread: " + Thread.CurrentThread.ManagedThreadId);
                    resetEvent.Set();
                });

            var proxy = service.StartHostAndProxy();

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            await proxy.VoidMethodDefinedAsync("test");

            Console.WriteLine("Contination thread: " + Thread.CurrentThread.ManagedThreadId);

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(2)))
                Assert.Fail("Callback never called");
        }

        [Test]
        public async Task CallAsync_MethodWithReturnValue()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("good")
                .Returns("BAD", "OK");

            service
                .TestMethod("second", "two")
                .Returns("2");

            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.MaximumRetries(1);
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

            var service = Substitute.For<ITestService>();
            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(
                    new Response {ResponseMessage = "test", StatusCode = 1},
                    new Response {ResponseMessage = "test", StatusCode = 0});

            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.MaximumRetries(1);
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

            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("good"))
                .Do(m =>
                {
                    Console.WriteLine("Callback thread: " + Thread.CurrentThread.ManagedThreadId);
                    resetEvent.Set();
                });

            var proxy = service.StartHostAndAsyncProxy();

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

            var service = Substitute.For<ITestService>();

            service
                .When(m => m.OneWay(Arg.Any<string>()))
                .Do(m =>
                {
                    Assert.That(m.Arg<string>(), Is.EqualTo("test"));

                    Console.WriteLine("Callback thread: " + Thread.CurrentThread.ManagedThreadId);
                    resetEvent.Set();
                });


            var proxy = service.StartHostAndAsyncProxy();

            await proxy.CallAsync(m => m.OneWay("test"));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }

        [Test]
        public void CallAsync_MultipleConcurrentCalls()
        {
            int iterations = 20;

            var service = Substitute.For<ITestService2>();

            service
                .TestMethod(Arg.Any<string>())
                .Returns(m => $"Echo: {m.Arg<string>()}")
                .AndDoes(m => Console.WriteLine($"Callback: {m.Arg<string>()}"));

            var proxy = service.StartHostAndAsyncProxy();

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
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("good")
                .Returns("OK");

            var proxy = service.StartHostAndAsyncProxy();

            string result = proxy.Client.TestMethod("good");
            Assert.That(result, Is.EqualTo("OK"));
        }

        [Test]
        public void CallAsync_CallingMethodWithByRefParams_ThrowsNotSupportedException()
        {
            var service = Substitute.For<IOutParamTestService>();

            byte[] expectedOutParam = { 0x01 };

            service
                .SingleOutParam(out expectedOutParam)
                .Returns(100);

            var proxy = service.StartHostAndAsyncProxy();

            byte[] resultingOutParam;

            Assert.That(() => proxy.CallAsync(m => m.SingleOutParam(out resultingOutParam)), Throws.TypeOf<NotSupportedException>());
        }
    }
}