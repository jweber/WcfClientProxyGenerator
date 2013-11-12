using System;

namespace WcfClientProxyGenerator
{
    internal interface IActionInvoker<out TServiceInterface>
        where TServiceInterface : class
    {
        void Invoke(Action<TServiceInterface> method, InvokeInfo invokeInfo = null);
        TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method, InvokeInfo invokeInfo = null);
    }
}
