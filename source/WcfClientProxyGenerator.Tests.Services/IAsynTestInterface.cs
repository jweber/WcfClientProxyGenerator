using System.ServiceModel;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract]
    public interface IAsyncService
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

    public class AsyncService : IAsyncService
    {
        public string ReturnMethod(string input) => input;


        public void VoidMethod(string input)
        { }

        public Task<string> ReturnMethodDefinedAsync(string input) => Task.FromResult(input);

        public Task VoidMethodDefinedAsync(string input) => Task.FromResult(0);

    }
}