using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Tests.WcfServiceHost.Services
{
    public class CustomAttributeService : ICustomAttributeService
    {
        public string Method(string input) => input;

        public string FaultMethod(string input) => input;

        public string KnownTypeMethod(string input) => input;
    }
}