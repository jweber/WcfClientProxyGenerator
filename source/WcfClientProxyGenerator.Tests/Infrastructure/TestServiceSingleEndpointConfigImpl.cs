using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public class TestServiceSingleEndpointConfigImpl : ITestServiceSingleEndpointConfig
    {
        private readonly Mock<ITestServiceSingleEndpointConfig> _mock;

        public TestServiceSingleEndpointConfigImpl()
        {
        }

        public TestServiceSingleEndpointConfigImpl(Mock<ITestServiceSingleEndpointConfig> mock)
        {
            _mock = mock;
        }

        public string TestMethod(string input)
        {
            if (_mock != null)
            {
                return _mock.Object.TestMethod(input);
            }

            return "test";
        }
    }
}
