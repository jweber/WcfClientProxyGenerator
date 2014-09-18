using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
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
}