using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Async;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    class AsyncExtensionTests
    {
        [Test]
        public async void AsyncExtension_MethodWithReturnValue()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            string result = await proxy.CallAsync(m => m.TestMethod("good"));
            Assert.AreEqual("OK", result);
        }

        [Test]
        public async void AsyncExtension_VoidMethod()
        {
            var mockService = new Mock<ITestService>();
            mockService
                .Setup(m => m.VoidMethod("good"))
                .Callback<string>(input =>
                {
                    Assert.That(input, Is.EqualTo("good"));
                });

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            await proxy.CallAsync(m => m.VoidMethod("good"));
        }
    }
}
