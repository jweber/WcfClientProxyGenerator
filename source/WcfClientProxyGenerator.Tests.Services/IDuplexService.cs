using System.ServiceModel;
using WcfClientProxyGenerator.Tests.Services.Infrastructure;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract(CallbackContract = typeof(IDuplexServiceCallback))]
    [ServicePath("/duplex")]
    public interface IDuplexService
    {
        [OperationContract]
        string Test(string input);

        [OperationContract(IsOneWay = true)]
        void OneWay(string input);
    }

    public interface IDuplexServiceCallback
    {
        [OperationContract]
        string TestCallback(string input);

        [OperationContract(IsOneWay = true)]
        void OneWayCallback(string input);
    }
}