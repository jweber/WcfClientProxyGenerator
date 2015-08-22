using System.ServiceModel;
using Moq;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class TestServiceImpl : ITestService
    {
        private readonly Mock<ITestService> _mock;

        public TestServiceImpl()
        {
        }

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

        public string TestMethod(string input, string two)
        {
            if (_mock != null)
                return _mock.Object.TestMethod(input, two);

            return string.Format("Echo: {0}, {1}", input, two);
        }

        public int TestMethodMixed(string input, int input2)
        {
            if (_mock != null)
                return _mock.Object.TestMethodMixed(input, input2);

            return input2;
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

            return new Response {ResponseMessage = string.Format("Echo: {0}", request.RequestMessage)};
        }

        public Response TestMethodComplexMulti(string input, Request request)
        {
            if (_mock != null)
                return _mock.Object.TestMethodComplexMulti(input, request);

            return new Response {ResponseMessage = string.Format("Echo: {0} {1}", input, request.RequestMessage)};
        }

        public void OneWay(string input)
        {
            if (_mock != null)
                _mock.Object.OneWay(input);
        }
    }
}