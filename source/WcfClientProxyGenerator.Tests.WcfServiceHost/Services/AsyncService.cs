using System.Threading.Tasks;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Tests.WcfServiceHost.Services
{
    public class AsyncService : IAsyncService
    {
        public string ReturnMethod(string input) => input;


        public void VoidMethod(string input)
        { }

        public Task<string> ReturnMethodDefinedAsync(string input) => Task.FromResult(input);

        public Task VoidMethodDefinedAsync(string input) => Task.FromResult(0);

    }
}