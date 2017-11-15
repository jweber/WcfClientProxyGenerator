using System;
using System.ServiceModel;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Tests.WcfServiceHost.Services
{
    public class TestService : ITestService
    {
        private static int sequenceCount = 0;
        
        public string Echo(string input) => input;

        public string Echo(string input, string secondInput) => input + " " + secondInput;

        public string EchoSequence(params string[] inputs) => inputs[(sequenceCount++ % inputs.Length)];

        public string UnhandledException() => throw new CommunicationException();

        public string FaultException() => throw new FaultException();

        public Response Complex(Request request, params Response[] responses) => responses == null ? new Response() : responses[(sequenceCount++ % responses.Length)];

        public void OneWay(string input)
        { }

        public void VoidMethod(string input)
        { }

        public string UnhandledExceptionOnFirstCallThenEcho(string input)
        {
            if (sequenceCount++ == 0)
                throw new Exception();

            return input;
        }

        public Response UnhandledExceptionOnFirstCall_Complex(Request request, Response response)
        {
            if (sequenceCount++ == 0)
                throw new Exception();

            return response;
        }

        public Response UnhandledExceptionOnFirstCall_ComplexMulti(string input, Request request, Response response)
        {
            if (sequenceCount++ == 0)
                throw new Exception();

            return response;
        }
    }
}