using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class DynamicProxyTypeGeneratorTests
    {
        [Test]
        public void Test()
        {
            var type = DynamicProxyTypeGenerator<ITestService2>
                .GenerateTypes<RetryingWcfActionInvokerProvider<ITestService2>>();
        }
    }
}
