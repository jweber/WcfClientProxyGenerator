using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Services.Issues
{
    [ServiceContract(
        Namespace = "SomeDomain.Contracts",
        Name = "SomeVendorServices")]
    public interface IIssue25Service
    {
        [OperationContract]
        Issue25Response GetOperation1(Issue25Request request);
    }

    public class Issue25Request {}
    public class Issue25Response {}
}