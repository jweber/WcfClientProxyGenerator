using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceContract]
    public interface ITestServiceSingleEndpointConfig
    {
        [OperationContract]
        string TestMethod(string input);
    }
}
