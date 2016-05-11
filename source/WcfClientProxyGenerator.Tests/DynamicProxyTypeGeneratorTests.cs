using System;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    #region Test support

    [ServiceContract]
    public interface IOverloadedService
    {
        [OperationContract]
        int Method(string input);

        [OperationContract(Name = "Method2")]
        int Method(string input, string input2);
    }

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

    [ServiceContract]
    public interface IAsyncTestInterface
    {
        [OperationContract]
        string ReturnMethod(string input);

        [OperationContract]
        void VoidMethod(string input);

        [OperationContract]
        Task<string> ReturnMethodDefinedAsync(string input);

        [OperationContract]
        Task VoidMethodDefinedAsync(string input);
    }

    [ServiceContract]
    public interface IOperationContractInterface
    {
        [OperationContract]
        string DefaultActionAndReplyAction();

        [OperationContract(Action = "NewAction")]
        string CustomAction();

        [OperationContract(ReplyAction = "NewReplyAction")]
        string CustomReplyAction();

        [OperationContract(Name = "NewName")]
        string CustomName();
    }

    [ServiceContract]
    public interface IAsyncTestInterface2
    {
        [OperationContract(Action = "Method", ReplyAction = "MethodResponse")]
        string Method(string input);

        [OperationContract(Action = "Method", ReplyAction = "MethodResponse")]
        Task<string> MethodAsync(string input);
    }

    [ServiceContract]
    public interface IAsyncTestInterface3
    {
        [OperationContract()]
        string Method(string input);

        [OperationContract(Action = "http://tempuri.org/IAsyncTestInterface3/Method", ReplyAction = "http://tempuri.org/IAsyncTestInterface3/MethodResponse")]
        Task<string> MethodAsync(string input);
    }

    #endregion
    
    [TestFixture]
    public class DynamicProxyTypeGeneratorTests
    {
        [SetUp]
        public void Setup()
        {
            DynamicProxyAssembly.Initialize();
        }

        private GeneratedTypes GenerateTypes<TServiceInterface>()
            where TServiceInterface : class
        {
            return DynamicProxyTypeGenerator<TServiceInterface>
                .GenerateTypes<RetryingWcfActionInvokerProvider<TServiceInterface>>();
        }

        [Test]
        public void ContractsWithOverloadedMethods_DoNotDuplicateSupportMethods()
        {
            Assert.That(() => this.GenerateTypes<IOverloadedService>(), Throws.Nothing);
        }

        [Test]
        public void CustomAttributes_AreCopiedTo_GeneratedInterface()
        {
            var types = this.GenerateTypes<ICustomAttributeService>();
            var attr = types.AsyncInterface.GetCustomAttribute<CustomServiceAttributeAttribute>();

            Assert.That(attr.Name, Is.EqualTo(CustomServiceAttributeAttribute.CtorArg));
            Assert.That(attr.Number, Is.EqualTo(CustomServiceAttributeAttribute.NumberProperty));
        }

        [Test]
        public void CustomAttributes_AreCopiedTo_GeneratedMethods()
        {
            var types = this.GenerateTypes<ICustomAttributeService>();

            var method = types.AsyncInterface
                .GetMethods()
                .First(m => m.Name.StartsWith(nameof(ICustomAttributeService.Method), StringComparison.Ordinal));

            var attr = method.GetCustomAttribute<CustomMethodAttributeAttribute>();

            Assert.That(attr.Name, Is.EqualTo(CustomMethodAttributeAttribute.CtorArg));
            Assert.That(attr.Number, Is.EqualTo(CustomMethodAttributeAttribute.NumberProperty));
        }

        [Test]
        public void FaultContractAttributes_AreNotCopiedTo_GeneratedAsyncMethods()
        {
            var types = this.GenerateTypes<ICustomAttributeService>();

            var method = types.AsyncInterface
                .GetMethods()
                .First(m => m.Name.StartsWith(nameof(ICustomAttributeService.FaultMethod), StringComparison.Ordinal));

            var attr = method.GetCustomAttribute<FaultContractAttribute>();

            Assert.That(attr, Is.Null);
        }

        [Test]
        public void AsyncInterface_AsyncMethodSignature_IsCreatedForSyncMethodWithReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("ReturnMethodAsync");
            Assert.That(asyncMethod, Is.Not.Null);
            Assert.That(asyncMethod.ReturnType, Is.EqualTo(typeof(Task<string>)));
        }

        [Test]
        public void AsyncInterface_AsyncMethodSignature_IsCreatedForSyncMethodWithoutReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("VoidMethodAsync");
            Assert.That(asyncMethod, Is.Not.Null);
            Assert.That(asyncMethod.ReturnType, Is.EqualTo(typeof(Task)));
        }

        [Test]
        public void AsyncInterface_AsyncMethodSignature_IsNotCreated_ForAlreadyAsyncMethodWithReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethods()
                .Where(m => m.Name.StartsWith("ReturnMethodDefinedAsync"));

            Assert.That(asyncMethod, Is.Null.Or.Empty);
        }

        [Test]
        public void AsyncInterface_AsyncMethodSignature_IsNotCreated_ForAlreadyAsyncMethodWithNoReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethods()
                .Where(m => m.Name.StartsWith("VoidMethodDefinedAsync"));

            Assert.That(asyncMethod, Is.Null.Or.Empty);
        }

        [Test]
        public void AsyncInterface_Default_ActionAndReplyAction_AttributeValuesAreGenerated()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("DefaultActionAndReplyActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.Action, Is.EqualTo("http://tempuri.org/IOperationContractInterface/DefaultActionAndReplyAction"));
            Assert.That(attr.ReplyAction, Is.EqualTo("http://tempuri.org/IOperationContractInterface/DefaultActionAndReplyActionResponse"));
        }

        [Test]
        public void AsyncInterface_DefaultAction_NamespaceIsDerivedFromDeclaringType()
        {
            var types = this.GenerateTypes<IChildService>();

            var childMethod = types.AsyncInterface.GetMethod("ChildMethodAsync");
            var childMethodAttr = childMethod.GetCustomAttribute<OperationContractAttribute>();

            var baseMethod = types.AsyncInterface.GetMethod("VoidMethodAsync");
            var baseMethodAttr = baseMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(childMethodAttr.Action, Is.EqualTo("http://tempuri.org/IChildService/ChildMethod"));
            Assert.That(baseMethodAttr.Action, Is.EqualTo("http://tempuri.org/ITestService/VoidMethod"));
        }

        [Test]
        public void AsyncInterface_DefaultReplyAction_NamespaceIsDerivedFromDeclaringType()
        {
            var types = this.GenerateTypes<IChildService>();

            var childMethod = types.AsyncInterface.GetMethod("ChildMethodAsync");
            var childMethodAttr = childMethod.GetCustomAttribute<OperationContractAttribute>();

            var baseMethod = types.AsyncInterface.GetMethod("VoidMethodAsync");
            var baseMethodAttr = baseMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(childMethodAttr.ReplyAction, Is.EqualTo("http://tempuri.org/IChildService/ChildMethodResponse"));
            Assert.That(baseMethodAttr.ReplyAction, Is.EqualTo("http://tempuri.org/ITestService/VoidMethodResponse"));
        }

        [Test]
        public void AsyncInterface_CustomAction_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.Action, Is.EqualTo("NewAction"));
        }

        [Test]
        public void AsyncInterface_CustomReplyAction_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomReplyActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.ReplyAction, Is.EqualTo("NewReplyAction"));
        }

        [Test]
        public void AsyncInterface_CustomName_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomNameAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.Name, Is.EqualTo("NewName"));
        }
        
        [Test]
        public void AsyncInterface_CustomName_IsUsedToBuildDefaultActionAndReplyAction()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomNameAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.Action, Is.EqualTo("http://tempuri.org/IOperationContractInterface/NewName"));
            Assert.That(attr.ReplyAction, Is.EqualTo("http://tempuri.org/IOperationContractInterface/NewNameResponse"));
        }

        [Test]
        public void AsyncMethodDefinition_NotGeneratedForNonAsyncMethod_WithExistingAsyncDefinition()
        {
            var types = this.GenerateTypes<IAsyncTestInterface2>();

            var generatedAsyncMethod = types.AsyncInterface.GetMethod("MethodAsync");
            Assert.That(generatedAsyncMethod, Is.Null);
        }

        [Test]
        public void AsyncMethodDefinition_NotGeneratedForNonAsyncMethod_WithExistingAsyncDefinition_NotUsingCustomActionAndReplyAction()
        {
            var types = this.GenerateTypes<IAsyncTestInterface3>();

            var generatedAsyncMethod = types.AsyncInterface.GetMethod("MethodAsync");
            Assert.That(generatedAsyncMethod, Is.Null);
        }
    }
}
