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
                .Setup(m => m.TestMethod("good"))
                .Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.CreateAsync<ITestService>(c =>
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            string result = await proxy.CallAsync(m => m.TestMethod("good"));
            Assert.That(result, Is.EqualTo("OK"));
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