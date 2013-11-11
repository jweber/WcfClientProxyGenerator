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

        /// <summary>
        /// True if method has returned and it has a non-void return value.
        /// </summary>
        public bool MethodHasReturnValue { get; set; }

        /// <summary>
        /// Return value from the service call. Only applicable when used
        /// in the OnAfterInvoke event handler and it is a non-void method.
        /// Throws if method is not returned yet (used in OnBeforeInvoke),
        /// or it is a void method.
        /// Use <see cref="MethodHasReturnValue" /> to tell if there is a value to be read.
        /// </summary>
        /// <exception cref="InvalidOperationException">If method has no return value</exception>
        public object ReturnValue
        {
            get
            {
                if (!MethodHasReturnValue)
                {
                    throw new InvalidOperationException("Cannot get return value; method has not returned yet or is a void method");
                }
                return _returnValue;
            }
            set
            {
                if (!MethodHasReturnValue)
                {
                    throw new InvalidOperationException("Cannot set return value; method has not returned yet or is a void method");
                }
                _returnValue = value;
            }
        }

        private object _returnValue;
    }
}
