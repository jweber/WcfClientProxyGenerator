namespace WcfClientProxyGenerator
{
    internal interface IActionInvokerProvider<out TServiceInterface>
        where TServiceInterface : class
    {
        IActionInvoker<TServiceInterface> ActionInvoker { get; }
    }
}
