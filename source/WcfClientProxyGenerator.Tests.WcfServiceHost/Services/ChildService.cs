using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Tests.WcfServiceHost.Services
{
    public class ChildService : TestService, IChildService
    {
        public string ChildMethod(string input) => input;
    }
}