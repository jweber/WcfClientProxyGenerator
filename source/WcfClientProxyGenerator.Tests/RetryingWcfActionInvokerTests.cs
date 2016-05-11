using System;
using System.ServiceModel;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
            var service = Substitute.For<ITestService>();
            
            var failResponse = new Response { ResponseMessage = "fail" };
            var successResponse = new Response { ResponseMessage = "success" };

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(failResponse, failResponse, successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)),
                retryCount: 2);

            actionInvoker.AddResponseToRetryOn<Response>(r => r.ResponseMessage == failResponse.ResponseMessage);

            var response = actionInvoker.Invoke(s => s.TestMethodComplex(new Request()));
            Assert.That(response.ResponseMessage, Is.EqualTo(successResponse.ResponseMessage));
        }

        [Test]
        public void AddResponseToRetryOn_RetriesOnConfiguredResponse_ForResponseBaseType()
        {
            var service = Substitute.For<ITestService>();

            var failResponse = new Response { StatusCode = 100 };
            var successResponse = new Response { StatusCode = 1 };

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(failResponse, failResponse, successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)),
                retryCount: 2);

            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == failResponse.StatusCode);

            var response = actionInvoker.Invoke(s => s.TestMethodComplex(new Request()));
            Assert.That(response.StatusCode, Is.EqualTo(successResponse.StatusCode));
        }

        [Test]
        public void AddResponseToRetryOn_RetriesOnMatchedPredicate_WhenMultiplePredicatesAreRegistered()
        {
            var service = Substitute.For<ITestService>();

            var firstFailResponse = new Response { StatusCode = 100 };
            var secondFailResponse = new Response { StatusCode = 101 };
            var successResponse = new Response { StatusCode = 1 };

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(firstFailResponse, secondFailResponse, successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
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
            var service = Substitute.For<ITestService>();

            service
                .TestMethod(Arg.Any<string>())
                .Throws<TimeoutException>();

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)));

            actionInvoker.AddExceptionToRetryOn<TimeoutException>(where: e => e.Message == "not the message");

            Assert.That(() => actionInvoker.Invoke(s => s.TestMethod("test")), Throws.TypeOf<TimeoutException>());
        }

        private void AssertThatCallRetriesOnException<TException>(
            Action<RetryingWcfActionInvoker<ITestService>> configurator = null,
            Func<TException> exceptionFactory = null)
            where TException : Exception, new()
        {
            var service = Substitute.For<ITestService>();

            if (exceptionFactory != null)
            {
                service
                    .TestMethod(Arg.Any<string>())
                    .Throws(exceptionFactory());
            }
            else
            {
                service
                    .TestMethod(Arg.Any<string>())
                    .Throws<TException>();
            }

            var delayPolicy = Substitute.For<IDelayPolicy>();

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => delayPolicy,
                retryCount: 5);

            configurator?.Invoke(actionInvoker);

            Assert.That(
                () => actionInvoker.Invoke(s => s.TestMethod("test")), 
                Throws.TypeOf<WcfRetryFailedException>());

            delayPolicy
                .Received(5)
                .GetDelay(Arg.Is<int>(i => i >= 0 && i <= 4));
        }
    }
}
