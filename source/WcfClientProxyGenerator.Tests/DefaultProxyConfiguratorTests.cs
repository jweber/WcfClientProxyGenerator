using NSubstitute;

using WcfClientProxyGenerator.Tests.Services;
using Xunit;

namespace WcfClientProxyGenerator.Tests
{
    public class DefaultProxyConfiguratorTests
    {
        #if NET45
        
        [Fact]
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
