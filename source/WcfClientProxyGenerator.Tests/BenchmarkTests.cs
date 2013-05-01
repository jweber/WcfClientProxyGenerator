using System.Diagnostics;
using System.Linq;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class BenchmarkTests
    {
        [Test, Ignore]
        public void Benchmark_RetryingWcfActionInvoker_Invoke()
        {
            var mockService = new Mock<ITestService>();

            mockService
                .Setup(m => m.TestMethodComplex(It.IsAny<Request>()))
                .Returns(new Response { StatusCode = 1 });

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => new TestServiceImpl(mockService));
            
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
