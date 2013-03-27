using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public class TestService2Impl : ITestService2
    {
        private readonly Mock<ITestService2> _mock;

        public TestService2Impl()
        {}

        public TestService2Impl(Mock<ITestService2> mock)
        {
            _mock = mock;
        }

        public string TestMethod(string input)
        {
            if (_mock != null)
                return _mock.Object.TestMethod(input);

            return string.Format("Echo: {0}", input);
        }
    }
}
