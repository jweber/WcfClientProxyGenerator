using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public class TrailingSlashOnNamespaceServiceImpl : ITrailingSlashOnNamespaceService
    {
        public string Echo(string input) => input;
    }

    [ServiceContract(Namespace = "http://example.com/services/")]
    public interface ITrailingSlashOnNamespaceService
    {
        [OperationContract]
        string Echo(string input);
    }
}