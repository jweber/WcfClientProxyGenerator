using System;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator.Async
{
    public static class AsyncExtensions
    {
        public static async Task<TResult> CallAsync<TServiceInterface, TResult>(
            this TServiceInterface proxy,
            Func<TServiceInterface, TResult> method)
            where TServiceInterface : class
        {
            return await Task.Run(() => method(proxy));
        }

        public static async Task CallAsync<TServiceInterface>(
            this TServiceInterface proxy,
            Action<TServiceInterface> method)
            where TServiceInterface : class
        {
            await Task.Run(() => method(proxy));
        }
    }
}
