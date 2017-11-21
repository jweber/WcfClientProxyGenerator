using WcfClientProxyGenerator.Tests.Services.Issues;

namespace WcfClientProxyGenerator.Tests.WcfServiceHost.Services.Issues
{
    public class Issue25Service : IIssue25Service
    {
        public Issue25Response GetOperation1(Issue25Request request)
        {
            return new Issue25Response();
        }
    }
}