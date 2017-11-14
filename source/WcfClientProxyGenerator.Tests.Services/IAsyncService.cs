using System.ServiceModel;
using System.Threading.Tasks;
using WcfClientProxyGenerator.Tests.Services.Infrastructure;

namespace WcfClientProxyGenerator.Tests.Services
{
    [ServiceContract]
    [ServicePath("/async")]
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
}