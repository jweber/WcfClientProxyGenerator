﻿using System;

namespace WcfClientProxyGenerator
{
    public interface IRetryingProxyConfigurator : IProxyConfigurator
    {
        void MaximumRetries(int retryCount);
        void TimeBetweenRetries(TimeSpan timeSpan);

        void RetryOnException<TException>(Predicate<TException> where = null)
            where TException : Exception;

        void RetryOnException(Type exceptionType, Predicate<Exception> where = null);

        void RetryOnResponse<TResponse>(Predicate<TResponse> where);
    }
}