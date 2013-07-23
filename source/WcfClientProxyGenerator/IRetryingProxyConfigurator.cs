using System;
using WcfClientProxyGenerator.Policy;

namespace WcfClientProxyGenerator
{
    public interface IRetryingProxyConfigurator : IProxyConfigurator
    {
        void MaximumRetries(int retryCount);
        
        void SetDelayPolicy(Func<IDelayPolicy> policyFactory);

        void RetryOnException<TException>(Predicate<TException> where = null)
            where TException : Exception;

        void RetryOnException(Type exceptionType, Predicate<Exception> where = null);

        void RetryOnResponse<TResponse>(Predicate<TResponse> where);
    }
}
