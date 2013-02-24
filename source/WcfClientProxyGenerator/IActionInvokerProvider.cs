using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator
{
    internal interface IActionInvokerProvider<out TServiceInterface>
        where TServiceInterface : class
    {
        IActionInvoker<TServiceInterface> ActionInvoker { get; }
    }
}
