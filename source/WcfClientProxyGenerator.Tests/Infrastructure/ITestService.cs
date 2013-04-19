using System.Runtime.Serialization;
using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceContract]
    public interface ITestService
    {
        [OperationContract]
        string TestMethod(string input);

        [OperationContract]
        void VoidMethod(string input);

        [OperationContract]
        Response TestMethodComplex(Request request);

        [OperationContract]
        Response TestMethodComplexMulti(string input, Request request);
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

    [DataContract]
    public class Response
    {
        [DataMember]
        public string ResponseMessage { get; set; }
    }
}
