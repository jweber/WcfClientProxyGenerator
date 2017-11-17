using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract]
    public interface ITestServiceSingleEndpointConfig
    {
        [OperationContract]
        string TestMethod(string input);
    }
}
