using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator
{
    internal interface IActionInvoker<out TServiceInterface>
        where TServiceInterface : class
    {
        void InvokeAction(Action<TServiceInterface> method);
        TResponse Invoke<TResponse>(Func<TServiceInterface, TResponse> method);
    }
}
