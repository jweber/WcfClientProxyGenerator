using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public static class AssertExt
    {
        public static void ThrowsAsync(
            Type exceptionType, 
            Func<Task> action, 
            IResolveConstraint messageAssert = null,
            string message = null)
        {
            var aggregateException = Assert.Throws<AggregateException>(() => action().Wait());

            Assert.That(
                aggregateException.InnerExceptions.Single(),
                Is.InstanceOf(exceptionType),
                message);

            if (messageAssert != null)
            {
                Assert.That(
                    aggregateException.InnerExceptions.Single().Message, 
                    messageAssert);
            }
        }

        public static void ThrowsAsync<TResult>(
            Type exceptionType, 
            Func<Task<TResult>> action, 
            IResolveConstraint messageAssert = null,
            string message = null)
        {
            var aggregateException = Assert.Throws<AggregateException>(() => action().Wait());

            Assert.That(
                aggregateException.InnerExceptions.Single(),
                Is.InstanceOf(exceptionType),
                message);

            if (messageAssert != null)
            {
                Assert.That(
                    aggregateException.InnerExceptions.Single().Message, 
                    messageAssert);
            }
        }
    }
}