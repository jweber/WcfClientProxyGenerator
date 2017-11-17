using System.ServiceModel;
using WcfClientProxyGenerator.Tests.Services.Infrastructure;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract(Namespace = "http://example.com/services/")]
    [ServicePath("/trailing-slash")]
    public interface ITrailingSlashOnNamespaceService
    {
        [OperationContract]
        string Echo(string input);
    }
}