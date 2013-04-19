using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public class TestServiceImpl : ITestService
    {
        private readonly Mock<ITestService> _mock;

        public TestServiceImpl()
        {}

        public TestServiceImpl(Mock<ITestService> mock)
        {
            _mock = mock;
        }

        public string TestMethod(string input)
        {
            if (_mock != null)
                return _mock.Object.TestMethod(input);

            return string.Format("Echo: {0}", input);
        }

        public void VoidMethod(string input)
        {
            if (_mock != null)
                _mock.Object.VoidMethod(input);
        }

        public Response TestMethodComplex(Request request)
        {
            if (_mock != null)
                return _mock.Object.TestMethodComplex(request);

            return new Response { ResponseMessage = string.Format("Echo: {0}", request.RequestMessage) };
        }

        public Response TestMethodComplexMulti(string input, Request request)
        {
            if (_mock != null)
                return _mock.Object.TestMethodComplexMulti(input, request);

            return new Response { ResponseMessage = string.Format("Echo: {0} {1}", input, request.RequestMessage) };
        }
    }
}
