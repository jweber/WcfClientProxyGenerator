using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceContract]
    public interface ITestService2
    {
        [OperationContract]
        string TestMethod(string input);
    }
}
