using System.ServiceModel;
using WcfClientProxyGenerator.Tests.Services.Infrastructure;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract]
    [ServicePath("/child")]
    public interface IChildService : ITestService
    {
        [OperationContract]
        string ChildMethod(string input);
    }
}
