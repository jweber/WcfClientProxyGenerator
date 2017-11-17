using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Tests.WcfServiceHost.Services
{
    public class TrailingSlashOnNamespaceService : ITrailingSlashOnNamespaceService
    {
        public string Echo(string input) => input;
    }
}