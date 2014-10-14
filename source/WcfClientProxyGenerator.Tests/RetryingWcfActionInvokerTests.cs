using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Policy;
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

        #region RetriesOnException

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

        [Test]
        public void AddExceptionToRetryOn_RetriesOnMatchedPredicate_WhenMultiplePredicatesAreRegistered()
        {
            this.AssertThatCallRetriesOnException<TestException>(
                c =>
                {
                    c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "test");
                    c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "other");
                },
                () => new TestException("test"));
            
            this.AssertThatCallRetriesOnException<TestException>(
                c =>
                {
                    c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "test");
                    c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "other");
                },
                () => new TestException("other"));
        }

        #endregion

        #region RetryOnResponseCondition

        [Test]
        public void AddResponseToRetryOn_RetriesOnConfiguredResponse_ForResponseType()
        {
            var mockService = new Mock<ITestService>();

            var failResponse = new Response { ResponseMessage = "fail" };
            var successResponse = new Response { ResponseMessage = "success" };

            mockService
                .SetupSequence(m => m.TestMethodComplex(It.IsAny<Request>()))
                .Returns(failResponse)
                .Returns(failResponse)
                .Returns(successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => new TestServiceImpl(mockService),
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)),
                retryCount: 2);

            actionInvoker.AddResponseToRetryOn<Response>(r => r.ResponseMessage == failResponse.ResponseMessage);

            var response = actionInvoker.Invoke(s => s.TestMethodComplex(new Request()));
            Assert.That(response.ResponseMessage, Is.EqualTo(successResponse.ResponseMessage));
        }

        [Test]
        public void AddResponseToRetryOn_RetriesOnConfiguredResponse_ForResponseBaseType()
        {
            var mockService = new Mock<ITestService>();

            var failResponse = new Response { StatusCode = 100 };
            var successResponse = new Response { StatusCode = 1 };

            mockService
                .SetupSequence(m => m.TestMethodComplex(It.IsAny<Request>()))
                .Returns(failResponse)
                .Returns(failResponse)
                .Returns(successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => new TestServiceImpl(mockService),
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)),
                retryCount: 2);

            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == failResponse.StatusCode);

            var response = actionInvoker.Invoke(s => s.TestMethodComplex(new Request()));
            Assert.That(response.StatusCode, Is.EqualTo(successResponse.StatusCode));
        }

        [Test]
        public void AddResponseToRetryOn_RetriesOnMatchedPredicate_WhenMultiplePredicatesAreRegistered()
        {
            var mockService = new Mock<ITestService>();

            var firstFailResponse = new Response { StatusCode = 100 };
            var secondFailResponse = new Response { StatusCode = 101 };
            var successResponse = new Response { StatusCode = 1 };

            mockService
                .SetupSequence(m => m.TestMethodComplex(It.IsAny<Request>()))
                .Returns(firstFailResponse)
                .Returns(secondFailResponse)
                .Returns(successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => new TestServiceImpl(mockService),
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)),
                retryCount: 2);

            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == firstFailResponse.StatusCode);
            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == secondFailResponse.StatusCode);

            var response = actionInvoker.Invoke(s => s.TestMethodComplex(new Request()));
            Assert.That(response.StatusCode, Is.EqualTo(successResponse.StatusCode));
        }

        #endregion

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

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => new TestServiceImpl(mockService),
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)));

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

            var mockDelayPolicy = new Mock<IDelayPolicy>();

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => new TestServiceImpl(mockService),
                () => mockDelayPolicy.Object,
                retryCount: 5);

            if (configurator != null)
            {
                configurator(actionInvoker);
            }

            Assert.That(
                () => actionInvoker.Invoke(s => s.TestMethod("test")), 
                Throws.TypeOf<WcfRetryFailedException>());

            mockDelayPolicy.Verify(
                m => m.GetDelay(It.IsInRange(0, 4, Range.Inclusive)), 
                Times.Exactly(5));
        }
    }
}
