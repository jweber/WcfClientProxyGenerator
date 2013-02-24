using System;

namespace WcfClientProxyGenerator
{
    internal interface IActionInvoker<out TServiceInterface>
        where TServiceInterface : class
    {
        TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method);
    }
}
