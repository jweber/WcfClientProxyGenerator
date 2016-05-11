using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceContract(Namespace = "http://example.com/services/")]
    public interface ITrailingSlashOnNamespaceService
    {
        [OperationContract]
        string Echo(string input);
    }
}