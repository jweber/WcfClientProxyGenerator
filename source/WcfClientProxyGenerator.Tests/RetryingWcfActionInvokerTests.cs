using System;
using System.ServiceModel;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using WcfClientProxyGenerator.Tests.Services;
using WcfClientProxyGenerator.Policy;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class RetryingWcfActionInvokerTests
    {
#if NET45
        
        [Test]
        public void Retries_OnChannelTerminatedException()
        {
            this.AssertThatCallRetriesOnException(() => new ChannelTerminatedException());
        }

#endif
    
        [Test]
        public void Retries_OnEndpointNotFoundException()
        {
            this.AssertThatCallRetriesOnException(() => new EndpointNotFoundException(""));
        }

        [Test]
        public void Retries_OnServerTooBusyException()
        {
            this.AssertThatCallRetriesOnException(() => new ServerTooBusyException(""));
        }

        #region RetriesOnException

        [Test]
        public void AddExceptionToRetryOn_RetriesOnConfiguredException()
        {
            this.AssertThatCallRetriesOnException(
                () => new TimeoutException(),
                c => c.AddExceptionToRetryOn<TimeoutException>());
        }

        [Test]
        public void AddExceptionToRetryOn_RetriesOnConfiguredException_WhenPredicateMatches()
        {
            this.AssertThatCallRetriesOnException(
                () => new TimeoutException(),
                c => c.AddExceptionToRetryOn<TimeoutException>(e => e.Message == "The operation has timed out."));
        }

        [Test]
        public void AddExceptionToRetryOn_RetriesOnConfiguredException_WhenPredicateMatches_UsingActualExceptionType()
        {
            this.AssertThatCallRetriesOnException<TestException>(
                () => new TestException("test"),
                c => c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "test"));
        }

        [Test]
        public void AddExceptionToRetryOn_RetriesOnMatchedPredicate_WhenMultiplePredicatesAreRegistered()
        {
            this.AssertThatCallRetriesOnException<TestException>(
                () => new TestException("test"),
                c =>
                {
                    c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "test");
                    c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "other");
                });
            
            this.AssertThatCallRetriesOnException<TestException>(
                () => new TestException("other"),
                c =>
                {
                    c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "test");
                    c.AddExceptionToRetryOn<TestException>(e => e.TestExceptionMessage == "other");
                });
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
                .Complex(Arg.Any<Request>())
                .Returns(failResponse, failResponse, successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)),
                retryCount: 2);

            actionInvoker.AddResponseToRetryOn<Response>(r => r.ResponseMessage == failResponse.ResponseMessage);

            var response = actionInvoker.Invoke(s => s.Complex(new Request()));
            Assert.That(response.ResponseMessage, Is.EqualTo(successResponse.ResponseMessage));
        }

        [Test]
        public void AddResponseToRetryOn_RetriesOnConfiguredResponse_ForResponseBaseType()
        {
            var service = Substitute.For<ITestService>();

            var failResponse = new Response { StatusCode = 100 };
            var successResponse = new Response { StatusCode = 1 };

            service
                .Complex(Arg.Any<Request>())
                .Returns(failResponse, failResponse, successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)),
                retryCount: 2);

            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == failResponse.StatusCode);

            var response = actionInvoker.Invoke(s => s.Complex(new Request()));
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
                .Complex(Arg.Any<Request>())
                .Returns(firstFailResponse, secondFailResponse, successResponse);

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)),
                retryCount: 2);

            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == firstFailResponse.StatusCode);
            actionInvoker.AddResponseToRetryOn<IResponseStatus>(r => r.StatusCode == secondFailResponse.StatusCode);

            var response = actionInvoker.Invoke(s => s.Complex(new Request()));
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
                .Echo(Arg.Any<string>())
                .Throws<TimeoutException>();

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(50)));

            actionInvoker.AddExceptionToRetryOn<TimeoutException>(where: e => e.Message == "not the message");

            Assert.That(() => actionInvoker.Invoke(s => s.Echo("test")), Throws.TypeOf<TimeoutException>());
        }

        private void AssertThatCallRetriesOnException<TException>(
            Func<TException> exceptionFactory,
            Action<RetryingWcfActionInvoker<ITestService>> configurator = null)
            where TException : Exception
        {
            var service = Substitute.For<ITestService>();

            service
                .Echo(Arg.Any<string>())
                .Throws(exceptionFactory());

            var delayPolicy = Substitute.For<IDelayPolicy>();

            var actionInvoker = new RetryingWcfActionInvoker<ITestService>(
                () => service,
                () => delayPolicy,
                retryCount: 5);

            configurator?.Invoke(actionInvoker);

            Assert.That(
                () => actionInvoker.Invoke(s => s.Echo("test")), 
                Throws.TypeOf<WcfRetryFailedException>());

            delayPolicy
                .Received(5)
                .GetDelay(Arg.Is<int>(i => i >= 0 && i <= 4));
        }
    }
}
