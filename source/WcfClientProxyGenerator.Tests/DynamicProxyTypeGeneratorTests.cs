using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using Moq;
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

    public class AsyncTestInterfaceImpl : IAsyncTestInterface
    {
        private readonly Mock<IAsyncTestInterface> _mock;

        public AsyncTestInterfaceImpl(Mock<IAsyncTestInterface> mock)
        {
            this._mock = mock;
        }

        public string ReturnMethod(string input)
        {
            return _mock.Object.ReturnMethod(input);
        }

        public void VoidMethod(string input)
        {
            _mock.Object.VoidMethod(input);
        }

        public Task<string> ReturnMethodDefinedAsync(string input)
        {
            return _mock.Object.ReturnMethodDefinedAsync(input);
        }

        public Task VoidMethodDefinedAsync(string input)
        {
            return _mock.Object.VoidMethodDefinedAsync(input);
        }
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
    }
}
