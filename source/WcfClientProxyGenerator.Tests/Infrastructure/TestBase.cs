using NUnit.Framework;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [TestFixture]
    public abstract class TestBase
    {
        [TearDown]
        public virtual void AfterEachTest()
        {
            InProcTestFactory.CloseHosts();
        }
    }
}
