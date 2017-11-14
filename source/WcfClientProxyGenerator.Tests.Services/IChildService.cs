using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract]
    public interface IChildService : ITestService
    {
        [OperationContract]
        string ChildMethod(string input);
    }
}
