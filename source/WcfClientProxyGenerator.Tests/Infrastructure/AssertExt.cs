using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public static class AssertExt
    {
        public static void ThrowsAsync(Type exceptionType, Func<Task> action)
        {
            var aggregateException = Assert.Throws<AggregateException>(() => action().Wait());

            Assert.That(
                aggregateException.InnerExceptions.Single(),
                Is.InstanceOf(exceptionType));
        }

        public static void ThrowsAsync<TResult>(Type exceptionType, Func<Task<TResult>> action)
        {
            var aggregateException = Assert.Throws<AggregateException>(() => action().Wait());
            
            Assert.That(
                aggregateException.InnerExceptions.Single(),
                Is.InstanceOf(exceptionType));
        }
    }
}