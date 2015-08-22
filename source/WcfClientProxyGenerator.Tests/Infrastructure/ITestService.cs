using System.Runtime.Serialization;
using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceContract]
    public interface ITestService
    {
        [OperationContract]
        string TestMethod(string input);

        [OperationContract(Name = "TestMethod2")]
        string TestMethod(string input, string two);

        [OperationContract]
        int TestMethodMixed(string input, int input2);

        [OperationContract]
        void VoidMethod(string input);

        [OperationContract]
        void VoidMethodNoParameters();

        [OperationContract]
        void VoidMethodIntParameter(int input);

        [OperationContract]
        int IntMethod();

        [OperationContract]
        Response TestMethodComplex(Request request);

        [OperationContract]
        Response TestMethodComplexMulti(string input, Request request);

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
