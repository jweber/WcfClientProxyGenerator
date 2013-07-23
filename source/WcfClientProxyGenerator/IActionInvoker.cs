using System;

namespace WcfClientProxyGenerator
{
    internal interface IActionInvoker<out TServiceInterface>
        where TServiceInterface : class
    {
        void Invoke(Action<TServiceInterface> method);
        TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method);
    }
}
