using System;
using Moq;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public class OutParamTestServiceImpl : IOutParamTestService
    {
        private readonly Mock<IOutParamTestService> _mock;

        public OutParamTestServiceImpl()
        {}

        public OutParamTestServiceImpl(Mock<IOutParamTestService> mock)
        {
            _mock = mock;
        }

        public int SingleOutParam(out byte[] out1)
        {
            if (_mock != null)
                return _mock.Object.SingleOutParam(out out1);

            out1 = new byte[] { 0x00, 0x01 };
            return 1;
        }

        public int MultipleOutParams(out byte[] out1, out string out2)
        {
            if (_mock != null)
                return _mock.Object.MultipleOutParams(out out1, out out2);

            out1 = new byte[] { 0x00, 0x01 };
            out2 = "message";
            return 1;
        }

        public int MixedParams(int inp1, out int out1, string inp2)
        {
            if (_mock != null)
                return _mock.Object.MixedParams(inp1, out out1, inp2);

            out1 = 24;
            return 1;
        }
    }
}