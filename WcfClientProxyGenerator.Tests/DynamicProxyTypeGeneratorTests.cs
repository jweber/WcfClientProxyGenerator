using System;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;

using Shouldly;
using WcfClientProxyGenerator.Tests.Services;
using Xunit;

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
    
    public class DynamicProxyTypeGeneratorTests
    {
        public DynamicProxyTypeGeneratorTests()
        {
            DynamicProxyAssembly.Initialize();
        }

        private GeneratedTypes GenerateTypes<TServiceInterface>()
            where TServiceInterface : class
        {
            return DynamicProxyTypeGenerator<TServiceInterface>
                .GenerateTypes<RetryingWcfActionInvokerProvider<TServiceInterface>>();
        }

        [Fact]
        public void ContractsWithOverloadedMethods_DoNotDuplicateSupportMethods()
        {
            Should.NotThrow(() => this.GenerateTypes<IOverloadedService>());
        }

        [Fact]
        public void CustomAttributes_AreCopiedTo_GeneratedInterface()
        {
            var types = this.GenerateTypes<ICustomAttributeService>();
            var attr = types.AsyncInterface.GetCustomAttribute<CustomServiceAttributeAttribute>();

            attr.Name.ShouldBe(CustomServiceAttributeAttribute.CtorArg);
            attr.Number.ShouldBe(CustomServiceAttributeAttribute.NumberProperty);
        }

        [Fact]
        public void CustomAttributes_AreCopiedTo_GeneratedMethods()
        {
            var types = this.GenerateTypes<ICustomAttributeService>();

            var method = types.AsyncInterface
                .GetMethods()
                .First(m => m.Name.StartsWith(nameof(ICustomAttributeService.Method), StringComparison.Ordinal));

            var attr = method.GetCustomAttribute<CustomMethodAttributeAttribute>();

            attr.Name.ShouldBe(CustomMethodAttributeAttribute.CtorArg);
            attr.Number.ShouldBe(CustomMethodAttributeAttribute.NumberProperty);
        }

        [Fact]
        public void FaultContractAttributes_AreNotCopiedTo_GeneratedAsyncMethods()
        {
            var types = this.GenerateTypes<ICustomAttributeService>();

            var method = types.AsyncInterface
                .GetMethods()
                .First(m => m.Name.StartsWith(nameof(ICustomAttributeService.FaultMethod), StringComparison.Ordinal));

            var attr = method.GetCustomAttribute<FaultContractAttribute>();

            attr.ShouldBeNull();
        }

        [Fact]
        public void AsyncInterface_AsyncMethodSignature_IsCreatedForSyncMethodWithReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("ReturnMethodAsync");
            asyncMethod.ShouldNotBeNull();
            asyncMethod.ReturnType.ShouldBe(typeof(Task<string>));
        }

        [Fact]
        public void AsyncInterface_AsyncMethodSignature_IsCreatedForSyncMethodWithoutReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("VoidMethodAsync");
            asyncMethod.ShouldNotBeNull();
            asyncMethod.ReturnType.ShouldBe(typeof(Task));
        }

        [Fact]
        public void AsyncInterface_AsyncMethodSignature_IsNotCreated_ForAlreadyAsyncMethodWithReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethods()
                .Where(m => m.Name.StartsWith("ReturnMethodDefinedAsync"));

            asyncMethod.ShouldBeEmpty();
        }

        [Fact]
        public void AsyncInterface_AsyncMethodSignature_IsNotCreated_ForAlreadyAsyncMethodWithNoReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethods()
                .Where(m => m.Name.StartsWith("VoidMethodDefinedAsync"));

            asyncMethod.ShouldBeEmpty();
        }

        [Fact]
        public void AsyncInterface_Default_ActionAndReplyAction_AttributeValuesAreGenerated()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("DefaultActionAndReplyActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            attr.Action.ShouldBe("http://tempuri.org/IOperationContractInterface/DefaultActionAndReplyAction");
            attr.ReplyAction.ShouldBe("http://tempuri.org/IOperationContractInterface/DefaultActionAndReplyActionResponse");
        }

        [Fact]
        public void AsyncInterface_DefaultAction_NamespaceIsDerivedFromDeclaringType()
        {
            var types = this.GenerateTypes<IChildService>();

            var childMethod = types.AsyncInterface.GetMethod("ChildMethodAsync");
            var childMethodAttr = childMethod.GetCustomAttribute<OperationContractAttribute>();

            var baseMethod = types.AsyncInterface.GetMethod("VoidMethodAsync");
            var baseMethodAttr = baseMethod.GetCustomAttribute<OperationContractAttribute>();

            childMethodAttr.Action.ShouldBe("http://tempuri.org/IChildService/ChildMethod");
            baseMethodAttr.Action.ShouldBe("http://tempuri.org/ITestService/VoidMethod");
        }

        [Fact]
        public void AsyncInterface_DefaultReplyAction_NamespaceIsDerivedFromDeclaringType()
        {
            var types = this.GenerateTypes<IChildService>();

            var childMethod = types.AsyncInterface.GetMethod("ChildMethodAsync");
            var childMethodAttr = childMethod.GetCustomAttribute<OperationContractAttribute>();

            var baseMethod = types.AsyncInterface.GetMethod("VoidMethodAsync");
            var baseMethodAttr = baseMethod.GetCustomAttribute<OperationContractAttribute>();

            childMethodAttr.ReplyAction.ShouldBe("http://tempuri.org/IChildService/ChildMethodResponse");
            baseMethodAttr.ReplyAction.ShouldBe("http://tempuri.org/ITestService/VoidMethodResponse");
        }

        [Fact]
        public void AsyncInterface_CustomAction_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            attr.Action.ShouldBe("NewAction");
        }

        [Fact]
        public void AsyncInterface_CustomReplyAction_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomReplyActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            attr.ReplyAction.ShouldBe("NewReplyAction");
        }

        [Fact]
        public void AsyncInterface_CustomName_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomNameAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            attr.Name.ShouldBe("NewName");
        }
        
        [Fact]
        public void AsyncInterface_CustomName_IsUsedToBuildDefaultActionAndReplyAction()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomNameAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            attr.Action.ShouldBe("http://tempuri.org/IOperationContractInterface/NewName");
            attr.ReplyAction.ShouldBe("http://tempuri.org/IOperationContractInterface/NewNameResponse");
        }

        [Fact]
        public void AsyncMethodDefinition_NotGeneratedForNonAsyncMethod_WithExistingAsyncDefinition()
        {
            var types = this.GenerateTypes<IAsyncTestInterface2>();

            var generatedAsyncMethod = types.AsyncInterface.GetMethod("MethodAsync");
            generatedAsyncMethod.ShouldBeNull();
        }

        [Fact]
        public void AsyncMethodDefinition_NotGeneratedForNonAsyncMethod_WithExistingAsyncDefinition_NotUsingCustomActionAndReplyAction()
        {
            var types = this.GenerateTypes<IAsyncTestInterface3>();

            var generatedAsyncMethod = types.AsyncInterface.GetMethod("MethodAsync");
            generatedAsyncMethod.ShouldBeNull();
        }
    }
}
