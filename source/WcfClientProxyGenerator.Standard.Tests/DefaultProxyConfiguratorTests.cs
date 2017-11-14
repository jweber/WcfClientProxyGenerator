using NSubstitute;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Standard.Tests
{
    [TestFixture]
    public class DefaultProxyConfiguratorTests
    {
        #if NET45
        
        [Test]
        public void SetEndpoint_IsCalledWith_FullNamespaceOfServiceInterface()
        {
            var config = Substitute.For<IRetryingProxyConfigurator>();

            DefaultProxyConfigurator.Configure<ITestService>(config);

            config
                .Received(1)
                .UseDefaultEndpoint();
        }
    
        #endif
    }
}
