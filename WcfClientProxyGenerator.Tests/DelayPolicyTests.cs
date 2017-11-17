using System;
using System.Linq;
using Shouldly;
using WcfClientProxyGenerator.Policy;
using Xunit;

namespace WcfClientProxyGenerator.Tests
{
    public class DelayPolicyTests
    {
        #region ConstantDelayPolicy

        [Fact]
        public void ConstantDelayPolicy_ReturnsSameDelay_ForRangeOfInput()
        {
            TimeSpan expectedDelay = TimeSpan.FromMilliseconds(100);
            var policy = new ConstantDelayPolicy(expectedDelay);

            foreach (int i in Enumerable.Range(0, 100))
            {
                policy.GetDelay(i).ShouldBe(expectedDelay);
            }
        }

        [Fact]
        public void ConstantDelayPolicy_ReturnsSameDelay_ForRandomInput()
        {
            TimeSpan expectedDelay = TimeSpan.FromSeconds(10);
            var policy = new ConstantDelayPolicy(expectedDelay);

            var random = new Random();
            foreach (int i in Enumerable.Range(0, 100))
            {
                policy.GetDelay(random.Next()).ShouldBe(expectedDelay);
            }
        }

        #endregion

        #region LinearBackoffDelayPolicy

        [Fact]
        public void LinearBackoffDelayPolicy_BacksOffLinearly()
        {
            TimeSpan minimumDelay = TimeSpan.FromSeconds(2);
            var policy = new LinearBackoffDelayPolicy(minimumDelay);

            policy.GetDelay(0).ShouldBe(TimeSpan.FromSeconds(2));
            policy.GetDelay(1).ShouldBe(TimeSpan.FromSeconds(4));
            policy.GetDelay(2).ShouldBe(TimeSpan.FromSeconds(6));
            policy.GetDelay(3).ShouldBe(TimeSpan.FromSeconds(8));
            policy.GetDelay(4).ShouldBe(TimeSpan.FromSeconds(10));
            policy.GetDelay(5).ShouldBe(TimeSpan.FromSeconds(12));
        }

        [Fact]
        public void LinearBackoffDelayPolicy_BacksOffLinearly_UntilReachingMaximumDelay()
        {
            TimeSpan minimumDelay = TimeSpan.FromSeconds(2);
            TimeSpan maximumDelay = TimeSpan.FromSeconds(7);
            var policy = new LinearBackoffDelayPolicy(minimumDelay, maximumDelay);

            policy.GetDelay(0).ShouldBe(TimeSpan.FromSeconds(2));
            policy.GetDelay(1).ShouldBe(TimeSpan.FromSeconds(4));
            policy.GetDelay(2).ShouldBe(TimeSpan.FromSeconds(6));
            policy.GetDelay(3).ShouldBe(TimeSpan.FromSeconds(7));
            policy.GetDelay(4).ShouldBe(TimeSpan.FromSeconds(7));
            policy.GetDelay(5).ShouldBe(TimeSpan.FromSeconds(7));
        }

        #endregion

        #region ExponentialBackoffDelayPolicy

        [Fact]
        public void ExponentialBackoffDelayPolicy_BacksOffExponentially()
        {
            TimeSpan minimumDelay = TimeSpan.FromSeconds(2);
            var policy = new ExponentialBackoffDelayPolicy(minimumDelay);

            policy.GetDelay(0).ShouldBe(TimeSpan.FromSeconds(2));
            policy.GetDelay(1).ShouldBe(TimeSpan.FromSeconds(4));
            policy.GetDelay(2).ShouldBe(TimeSpan.FromSeconds(8));
            policy.GetDelay(3).ShouldBe(TimeSpan.FromSeconds(16));
            policy.GetDelay(4).ShouldBe(TimeSpan.FromSeconds(32));
            policy.GetDelay(5).ShouldBe(TimeSpan.FromSeconds(64));
            policy.GetDelay(6).ShouldBe(TimeSpan.FromSeconds(128));
        }

        [Fact]
        public void ExponentialBackoffDelayPolicy_BacksOffExponentially_UntilReachingMaximumDelay()
        {
            TimeSpan minimumDelay = TimeSpan.FromSeconds(2);
            TimeSpan maximumDelay = TimeSpan.FromSeconds(20);
            var policy = new ExponentialBackoffDelayPolicy(minimumDelay, maximumDelay);

            policy.GetDelay(0).ShouldBe(TimeSpan.FromSeconds(2));
            policy.GetDelay(1).ShouldBe(TimeSpan.FromSeconds(4));
            policy.GetDelay(2).ShouldBe(TimeSpan.FromSeconds(8));
            policy.GetDelay(3).ShouldBe(TimeSpan.FromSeconds(16));
            policy.GetDelay(4).ShouldBe(TimeSpan.FromSeconds(20));
            policy.GetDelay(5).ShouldBe(TimeSpan.FromSeconds(20));
            policy.GetDelay(6).ShouldBe(TimeSpan.FromSeconds(20));
        }

        #endregion
    }
}