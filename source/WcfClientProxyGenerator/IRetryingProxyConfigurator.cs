using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace WcfClientProxyGenerator
{
    public interface IProxyConfigurator
    {
        void SetEndpoint(string endpointConfigurationName);
        void SetEndpoint(Binding binding, EndpointAddress endpointAddress);
    }

    public interface IRetryingProxyConfigurator : IProxyConfigurator
    {
        void MaximumRetries(int retryCount);
        void TimeBetweenRetries(TimeSpan timeSpan);

        void AddExceptionToRetryOn<TException>(Predicate<Exception> where = null)
            where TException : Exception;

        void AddExceptionToRetryOn(Type exceptionType, Predicate<Exception> where = null);
    }
}
