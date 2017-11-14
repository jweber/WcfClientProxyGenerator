using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Standard.Tests
{
    [TestFixture]
    public class AsyncTests : TestBase
    {
        [Test]
        public async Task ServiceContractDefinedAsyncMethod_WithReturnValue()
        {
            var proxy = GenerateProxy<IAsyncService>();

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            string result = await proxy.ReturnMethodDefinedAsync("test");

            Console.WriteLine("Contination thread: " + Thread.CurrentThread.ManagedThreadId);

            Assert.That(result, Is.EqualTo("test"));
        }
        
        [Test]
        public async Task ServiceContractDefinedAsyncMethod_WithNoReturnValue()
        {
            var proxy = GenerateProxy<IAsyncService>();

            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            await proxy.VoidMethodDefinedAsync("test");

            Console.WriteLine("Contination thread: " + Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public async Task CallAsync_MethodWithReturnValue()
        {
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.MaximumRetries(1);
                c.RetryOnResponse<string>(s => s == "bad");
            });
            
            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            string result = await proxy.CallAsync(m => m.EchoSequence("bad", "good"));
            string result2 = await proxy.CallAsync(m => m.Echo("hello", "world"));
            
            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            Assert.That(result, Is.EqualTo("good"));
            Assert.That(result2, Is.EqualTo("hello world"));
        }

        [Test]
        public async Task CallAsync_MethodWithReturnValue2()
        {
            var request = new Request() { RequestMessage = "test" };

            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.MaximumRetries(1);
                c.RetryOnResponse<Response>(s => s.StatusCode == 1);
            });
            
            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            var result = await proxy.CallAsync(m => m.Complex(request, 
                new Response { StatusCode = 1, ResponseMessage = "bad" },
                new Response { StatusCode = 0, ResponseMessage = "test" }));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);

            Assert.That(result.StatusCode, Is.EqualTo(0));
            Assert.That(result.ResponseMessage, Is.EqualTo("test"));
        }
        
        [Test]
        public async Task CallAsync_VoidMethod()
        {
            var proxy = GenerateAsyncProxy<IAsyncService>();
            
            Console.WriteLine("Caller thread: " + Thread.CurrentThread.ManagedThreadId);

            await proxy.CallAsync(m => m.VoidMethod("good"));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public async Task CallAsync_OneWayOperation()
        {
            var proxy = GenerateAsyncProxy<ITestService>();

            await proxy.CallAsync(m => m.OneWay("test"));

            Console.WriteLine("Continuation thread: " + Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public void CallAsync_MultipleConcurrentCalls()
        {
            int iterations = 20;

            var proxy = GenerateAsyncProxy<ITestService>();
            
            var tasks = new List<Task>();
            for (int i = 0; i < iterations; i++)
            {
                int i1 = i;
                tasks.Add(proxy.CallAsync(m => m.Echo(i1.ToString())));
                //Console.WriteLine("Queued task: " + i);
            }

            //Console.WriteLine("Waiting tasks...");

            Task.WaitAll(tasks.ToArray());

            for (int i = 0; i < iterations; i++)
            {
                string result = ((Task<string>) tasks[i]).Result;
                Assert.That(result, Is.EqualTo(i.ToString()));
            }
        }

        [Test]
        public void CallAsync_CanCallIntoSyncProxy()
        {
            var proxy = GenerateAsyncProxy<ITestService>();
            
            string result = proxy.Client.Echo("good");
            Assert.That(result, Is.EqualTo("good"));
        }

        [Test]
        public void CallAsync_CallingMethodWithByRefParams_ThrowsNotSupportedException()
        {
            byte[] expectedOutParam = { 0x01 };

            var proxy = GenerateAsyncProxy<IOutParamTestService>();
            
            byte[] resultingOutParam;

            Assert.That(() => proxy.CallAsync(m => m.SingleOutParam(out resultingOutParam)), Throws.TypeOf<NotSupportedException>());
        }
    }
}