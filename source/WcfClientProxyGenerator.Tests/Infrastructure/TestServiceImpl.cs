using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Moq;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
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

        public void VoidMethodNoParameters()
        {
            if (_mock != null)
                _mock.Object.VoidMethodNoParameters();
        }

        public void VoidMethodIntParameter(int input)
        {
            if (_mock != null)
                _mock.Object.VoidMethodIntParameter(input);
        }

        public int IntMethod()
        {
            if (_mock != null)
                return _mock.Object.IntMethod();

            return 42;
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
