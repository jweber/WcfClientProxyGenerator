using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using WcfClientProxyGenerator.Tests.Services.Infrastructure;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract]
    [ServicePath("/test")]
    public interface ITestService
    {
        [OperationContract]
        string Echo(string input);

        [OperationContract(Name = "Echo2")]
        string Echo(string input, string secondInput);

        [OperationContract]
        string EchoSequence(params string[] inputs);

        [OperationContract]
        string UnhandledException();
        
        [OperationContract]
        Response Complex(Request request, params Response[] responses);
        
        [OperationContract(IsOneWay = true)]
        void OneWay(string input);
    }

    [DataContract]
    public class Request
    {
        [DataMember]
        public string RequestMessage { get; set; }

        protected bool Equals(Request other)
        {
            return string.Equals(RequestMessage, other.RequestMessage);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != this.GetType())
            {
                return false;
            }
            return Equals((Request) obj);
        }

        public override int GetHashCode()
        {
            return (RequestMessage != null ? RequestMessage.GetHashCode() : 0);
        }
    }
    
    public interface IResponseStatus
    {
        int StatusCode { get; }
    }
    
    [DataContract]
    public class Response : IResponseStatus
    {
        [DataMember]
        public string ResponseMessage { get; set; }

        [DataMember]
        public int StatusCode { get; set; }
    }
}