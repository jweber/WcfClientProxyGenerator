using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
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
        public void AsyncMethodSignature_IsCreatedForSyncMethodWithReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("ReturnMethodAsync");
            Assert.That(asyncMethod, Is.Not.Null);
            Assert.That(asyncMethod.ReturnType, Is.EqualTo(typeof(Task<string>)));
        }

        [Test]
        public void AsyncMethodSignature_IsCreatedForSyncMethodWithoutReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("VoidMethodAsync");
            Assert.That(asyncMethod, Is.Not.Null);
            Assert.That(asyncMethod.ReturnType, Is.EqualTo(typeof(Task)));
        }

        [Test]
        public void AsyncMethodSignature_IsNotCreated_ForAlreadyAsyncMethodWithReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethods()
                .Where(m => m.Name.StartsWith("ReturnMethodDefinedAsync"));

            Assert.That(asyncMethod, Is.Null.Or.Empty);
        }

        [Test]
        public void AsyncMethodSignature_IsNotCreated_ForAlreadyAsyncMethodWithNoReturnValue()
        {
            var types = this.GenerateTypes<IAsyncTestInterface>();

            var asyncMethod = types.AsyncInterface.GetMethods()
                .Where(m => m.Name.StartsWith("VoidMethodDefinedAsync"));

            Assert.That(asyncMethod, Is.Null.Or.Empty);
        }

        [Test]
        public void Default_ActionAndReplyAction_AttributeValuesAreGenerated()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("DefaultActionAndReplyActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.Action, Is.EqualTo("http://tempuri.org/IOperationContractInterface/DefaultActionAndReplyAction"));
            Assert.That(attr.ReplyAction, Is.EqualTo("http://tempuri.org/IOperationContractInterface/DefaultActionAndReplyActionResponse"));
        }

        [Test]
        public void CustomAction_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.Action, Is.EqualTo("NewAction"));
        }

        [Test]
        public void CustomReplyAction_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomReplyActionAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.ReplyAction, Is.EqualTo("NewReplyAction"));
        }

        [Test]
        public void CustomName_IsUsedOnAsyncMethod()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomNameAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.Name, Is.EqualTo("NewName"));
        }
        
        [Test]
        public void CustomName_IsUsedToBuildDefaultActionAndReplyAction()
        {
            var types = this.GenerateTypes<IOperationContractInterface>();

            var asyncMethod = types.AsyncInterface.GetMethod("CustomNameAsync");

            var attr = asyncMethod.GetCustomAttribute<OperationContractAttribute>();

            Assert.That(attr.Action, Is.EqualTo("http://tempuri.org/IOperationContractInterface/NewName"));
            Assert.That(attr.ReplyAction, Is.EqualTo("http://tempuri.org/IOperationContractInterface/NewNameResponse"));
        }
    }
}
