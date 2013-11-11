using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Provides information on the invoked method.
    /// </summary>
    public class InvokeInfo
    {
        /// <summary>
        /// Name of the called method.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Parameters passed to the method.
        /// </summary>
        public object[] Parameters { get; set; }
    }
}
