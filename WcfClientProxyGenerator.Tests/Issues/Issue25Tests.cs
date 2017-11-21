using Shouldly;
using WcfClientProxyGenerator.Tests.Infrastructure;
using WcfClientProxyGenerator.Tests.Services.Issues;
using Xunit;

namespace WcfClientProxyGenerator.Tests.Issues
{
    public class Issue25Tests : TestBase
    {
        [Fact]
        public void Test()
        {
            Should.NotThrow(() => WcfClientProxy.Create<IIssue25Service>(this.TestServer.Binding, this.TestServer.BaseAddress));
        }
    }
}