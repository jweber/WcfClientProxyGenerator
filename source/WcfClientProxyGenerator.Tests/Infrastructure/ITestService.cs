using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceContract]
    public interface ITestService
    {
        [OperationContract]
        string TestMethod(string input);

//        [OperationContract]
//        Response TestMethodComplex(Request request);
    }

    [DataContract]
    public class Request
    {
        [DataMember]
        public string RequestMessage { get; set; }
    }

    [DataContract]
    public class Response
    {
        [DataMember]
        public string ResponseMessage { get; set; }
    }
}
