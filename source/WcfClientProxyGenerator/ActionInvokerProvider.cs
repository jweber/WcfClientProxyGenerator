using System.ServiceModel;
using System.ServiceModel.Channels;
using JetBrains.Annotations;

namespace WcfClientProxyGenerator
{
    internal class ActionInvokerProvider<TServiceInterface>
        where TServiceInterface : class
    {
        private readonly Binding _binding;
        private readonly EndpointAddress _endpointAddress;

        protected ActionInvokerProvider(Binding binding, EndpointAddress endpointAddress)
        {
            _binding = binding;
            _endpointAddress = endpointAddress;
        }

        [UsedImplicitly]
        protected IActionInvoker<TServiceInterface> ActionInvoker
        {
            get
            {
                var actionInvoker = new RetryingWcfActionInvoker<TServiceInterface>(
                    () => new ChannelFactory<TServiceInterface>(_binding, _endpointAddress).CreateChannel());

                return actionInvoker;
            }
        }
    }
}