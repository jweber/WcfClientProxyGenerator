using System;
using System.ServiceModel;
using WcfClientProxyGenerator.Tests.Services.Infrastructure;

namespace WcfClientProxyGenerator.Tests.Services
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class CustomServiceAttributeAttribute : Attribute
    {
        public const string CtorArg = "hello world";
        public const int NumberProperty = 100;

        public CustomServiceAttributeAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int Number { get; set; }
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public class CustomMethodAttributeAttribute : Attribute
    {
        public const string CtorArg = "method";
        public const int NumberProperty = 200;

        public CustomMethodAttributeAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int Number { get; set; }
    }
    
    [ServiceContract]
    [CustomServiceAttribute(CustomServiceAttributeAttribute.CtorArg, Number = CustomServiceAttributeAttribute.NumberProperty)]
    [ServicePath("/custom-attribute")]
    public interface ICustomAttributeService
    {
        [OperationContract]
        [CustomMethodAttribute(CustomMethodAttributeAttribute.CtorArg, Number = CustomMethodAttributeAttribute.NumberProperty)]
        string Method(string input);

        [OperationContract]
        [FaultContract(typeof(Exception))]
        string FaultMethod(string input);

        [OperationContract]
        [ServiceKnownType(typeof(string))]
        string KnownTypeMethod(string input);
    }
}