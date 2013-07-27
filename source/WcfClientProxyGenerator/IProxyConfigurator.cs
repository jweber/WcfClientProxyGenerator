using System.ServiceModel;
using System.ServiceModel.Channels;

namespace WcfClientProxyGenerator
{
    public interface IProxyConfigurator
    {
        void UseDefaultEndpoint();
        void SetEndpoint(string endpointConfigurationName);
        void SetEndpoint(Binding binding, EndpointAddress endpointAddress);
    }
}