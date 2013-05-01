using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class DefaultProxyConfiguratorTests
    {
        [Test]
        public void SetEndpoint_IsCalledWith_FullNamespaceOfServiceInterface()
        {
            var mockProxy = new Mock<IRetryingProxyConfigurator>();

            DefaultProxyConfigurator.Configure<ITestService>(mockProxy.Object);

            mockProxy.Verify(
                m => m.SetEndpoint(typeof(ITestService).FullName), 
                Times.Once());
        }
    }
}
