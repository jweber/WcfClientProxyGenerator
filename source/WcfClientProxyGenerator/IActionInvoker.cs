using System;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator
{
    internal interface IActionInvoker<out TServiceInterface>
        where TServiceInterface : class
    {
        void Invoke(Action<TServiceInterface> method, InvokeInfo invokeInfo = null);
        TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method, InvokeInfo invokeInfo = null);

        Task InvokeAsync(Func<TServiceInterface, Task> method, InvokeInfo invokeInfo = null);
        Task<TResponse> InvokeAsync<TResponse>(Func<TServiceInterface, Task<TResponse>> method, InvokeInfo invokeInfo = null);
    }
}
