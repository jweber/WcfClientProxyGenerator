using System.ServiceModel;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests.Issues
{
    [TestFixture]
    public class Issue25
    {
        [Test]
        public void Test()
        {
            var service = Substitute.For<ITestService>();
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(service);
            
            Assert.DoesNotThrow(() => WcfClientProxy.Create<IIssue25Service>(
                c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress)));
        }

        [ServiceContract(
            Namespace = "SomeDomain.Contracts",
            Name = "SomeVendorServices")]
        public interface IIssue25Service
        {
            [OperationContract]
            Issue25Response GetOperation1(Issue25Request request);
        }

        public class Issue25Request
        { }

        public class Issue25Response
        { }
    }
}