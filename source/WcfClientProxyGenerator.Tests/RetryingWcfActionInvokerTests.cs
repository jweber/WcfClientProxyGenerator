using System;
using System.ServiceModel;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class RetryingWcfActionInvokerTests
    {
        [Test]
        public void Retries_OnChannelTerminatedException()
        {
            this.AssertThatCallRetriesOnException<ChannelTerminatedException>();
        }

        [Test]
        public void Retries_OnEndpointNotFoundException()
        {
            this.AssertThatCallRetriesOnException<EndpointNotFoundException>();
        }

        [Test]
        public void Retries_OnServerTooBusyException()
        {
            this.AssertThatCallRetriesOnException<ServerTooBusyException>();
        }
        
        [Test]
        public void AddExceptionToRetryOn_RetriesOnConfiguredException()
        {
            this.AssertThatCallRetriesOnException<TimeoutException>(
                c => c.AddExceptionToRetryOn<TimeoutException>());
        }

        [Test]
        public void AddExceptionToRetryOn_RetriesOnConfiguredException_WhenPredicateMatches()
        {
            this.AssertThatCallRetriesOnException<TimeoutException>(
                c => c.AddExceptionToRetryOn<TimeoutException>(e => e.Message == "The operation has timed out."));
        }

        [Test]
        public void AddExceptionToRetryOn_RetriesOnConfiguredException_WhenPredicateMatches_UsingActualExceptionType()
        {
            this.AssertThatCallRetriesOnException<TestException>(
                c => c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "test"),
                () => new TestException("test"));
        }

        public class TestException : Exception
        {
            public TestException()
            {}

            public TestException(string testExceptionMessage)
            {
                TestExceptionMessage = testExceptionMessage;
            }

            public string TestExceptionMessage { get; set; }
        }

        [Test]
        public void AddExceptionToRetryOn_PassesThroughException_OnConfiguredException_WhenPredicateDoesNotMatch()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod(It.IsAny<string>())).Throws<TimeoutException>();

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(() => new TestServiceImpl(mockService));
            actionInvoker.AddExceptionToRetryOn<TimeoutException>(where: e => e.Message == "not the message");

            Assert.That(() => actionInvoker.Invoke(s => s.TestMethod("test")), Throws.TypeOf<TimeoutException>());
        }

        private void AssertThatCallRetriesOnException<TException>(
            Action<RetryingWcfActionInvoker<ITestService>> configurator = null,
            Func<TException> exceptionFactory = null)
            where TException : Exception, new()
        {
            var mockService = new Mock<ITestService>();

            if (exceptionFactory != null)
            {
                mockService.Setup(m => m.TestMethod(It.IsAny<string>())).Throws(exceptionFactory());
            }
            else 
            {
                mockService.Setup(m => m.TestMethod(It.IsAny<string>())).Throws<TException>();
            }

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(() => new TestServiceImpl(mockService));
            if (configurator != null)
            {
                configurator(actionInvoker);
            }
            
            Assert.That(
                () => actionInvoker.Invoke(s => s.TestMethod("test")), 
                Throws.TypeOf<WcfRetryFailedException>());
        }
    }
}
