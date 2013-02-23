using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
