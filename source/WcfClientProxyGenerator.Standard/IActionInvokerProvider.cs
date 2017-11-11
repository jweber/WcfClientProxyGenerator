namespace WcfClientProxyGenerator.Standard
{
    internal interface IActionInvokerProvider<out TServiceInterface>
        where TServiceInterface : class
    {
        IActionInvoker<TServiceInterface> ActionInvoker { get; }
    }
}
