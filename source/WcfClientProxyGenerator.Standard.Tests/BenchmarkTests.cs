using System.Diagnostics;
using System.Linq;
using System.Threading;
using NSubstitute;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Standard.Tests
{
    [TestFixture]
    public class BenchmarkTests : TestBase
    {
        [Test, Explicit]
        public void Benchmark_RetryingWcfActionInvoker_Invoke()
        {
            var proxy = GenerateProxy<ITestService>();
            
            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => proxy);
            
            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == 100);

            var sw = Stopwatch.StartNew();
            foreach (var i in Enumerable.Range(0, 300000))
            {
                var response = actionInvoker.Invoke(s => s.Complex(new Request(), new Response { StatusCode = 1 }));
                Assert.That(response.StatusCode, Is.EqualTo(1));
            }
            sw.Stop();
            
            Trace.WriteLine(string.Format("Complete in {0}", sw.Elapsed));
        }
    }
}
