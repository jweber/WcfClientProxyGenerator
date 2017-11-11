using System;
using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract]
    public interface ITestService
    {
        [OperationContract]
        string Echo(string input);

        [OperationContract]
        string UnhandledException();
    }

    public class TestService : ITestService
    {
        public string Echo(string input) => input;

        public string UnhandledException() => throw new CommunicationException();
    }
}