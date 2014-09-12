using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceContract]
    public interface ITestService2
    {
        [OperationContract(Action = "TestMethod", ReplyAction = "*")]
        string TestMethod(string input);
    }
}
