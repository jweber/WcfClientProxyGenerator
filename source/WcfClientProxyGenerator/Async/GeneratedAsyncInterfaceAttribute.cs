using System;

namespace WcfClientProxyGenerator.Async
{
    /// <summary>
    /// Marker attribute placed on runtime generated *Async ServiceContract interfaces
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    internal class GeneratedAsyncInterfaceAttribute : Attribute
    {}
}