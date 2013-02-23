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
            this._mock = mock;
        }

        public string TestMethod(string input)
        {
            if (this._mock != null)
            {
                return this._mock.Object.TestMethod(input);    
            }

            return string.Format("Echo: {0}", input);
        }

//        public Response TestMethodComplex(Request request)
//        {
//            if (this._mock != null)
//            {
//                return this._mock.Object.TestMethodComplex(request);
//            }
//
//            return new Response { ResponseMessage = string.Format("Echo: {0}", request.RequestMessage) };
//        }
    }
}
