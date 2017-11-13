using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract]
    public interface IOutParamTestService
    {
        [OperationContract]
        int SingleOutParam(out byte[] output);

        [OperationContract]
        int MultipleOutParams(out byte[] out1, out string out2);

        [OperationContract]
        int MixedParams(int inp1, out int out1, string inp2);
    }

    public class OutParamsTestService : IOutParamTestService
    {
        public int SingleOutParam(out byte[] output)
        {
            output = new byte[] { 0x01 };
            return 1;
        }

        public int MultipleOutParams(out byte[] out1, out string out2)
        {
            out1 = new byte[] { 0x01 };
            out2 = "hello world";
            
            return 1;
        }

        public int MixedParams(int inp1, out int out1, string inp2)
        {
            out1 = inp1;
            
            return 1;
        }
    }
}