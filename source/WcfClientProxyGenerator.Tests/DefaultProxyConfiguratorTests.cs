using NSubstitute;
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
            var config = Substitute.For<IRetryingProxyConfigurator>();

            DefaultProxyConfigurator.Configure<ITestService>(config);

            config
                .Received(1)
                .UseDefaultEndpoint();
        }
    }
}
