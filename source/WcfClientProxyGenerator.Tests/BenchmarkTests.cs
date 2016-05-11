using System.Diagnostics;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class BenchmarkTests
    {
        [Test, Explicit]
        public void Benchmark_RetryingWcfActionInvoker_Invoke()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(new Response { StatusCode = 1 });

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service);
            
            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == 100);

            var sw = Stopwatch.StartNew();
            foreach (var i in Enumerable.Range(0, 300000))
            {
                var response = actionInvoker.Invoke(s => s.TestMethodComplex(new Request()));
                Assert.That(response.StatusCode, Is.EqualTo(1));
            }
            sw.Stop();
            
            Trace.WriteLine(string.Format("Complete in {0}", sw.Elapsed));
        }
    }
}
