using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator
{
    public interface IProxyConfigurator
    {
        void MaximumRetries(int retryCount);
        void TimeBetweenRetries(TimeSpan timeSpan);

        void AddExceptionToRetryOn<TException>(Predicate<Exception> where = null)
            where TException : Exception;

        void AddExceptionToRetryOn(Type exceptionType, Predicate<Exception> where = null);
    }
}
