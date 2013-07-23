using System;
using System.Linq;
using NUnit.Framework;
using WcfClientProxyGenerator.Policy;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class DelayPolicyTests
    {
        #region ConstantDelayPolicy

        [Test]
        public void ConstantDelayPolicy_ReturnsSameDelay_ForRangeOfInput()
        {
            TimeSpan expectedDelay = TimeSpan.FromMilliseconds(100);
            var policy = new ConstantDelayPolicy(expectedDelay);

            foreach (int i in Enumerable.Range(0, 100))
            {
                Assert.AreEqual(expectedDelay, policy.GetDelay(i));
            }
        }

        [Test]
        public void ConstantDelayPolicy_ReturnsSameDelay_ForRandomInput()
        {
            TimeSpan expectedDelay = TimeSpan.FromSeconds(10);
            var policy = new ConstantDelayPolicy(expectedDelay);

            var random = new Random();
            foreach (int i in Enumerable.Range(0, 100))
            {
                Assert.AreEqual(expectedDelay, policy.GetDelay(random.Next()));
            }
        }

        #endregion

        #region LinearBackoffDelayPolicy

        [Test]
        public void LinearBackoffDelayPolicy_BacksOffLinearly()
        {
            TimeSpan minimumDelay = TimeSpan.FromSeconds(2);
            var policy = new LinearBackoffDelayPolicy(minimumDelay);

            Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(0));
            Assert.AreEqual(TimeSpan.FromSeconds(4), policy.GetDelay(1));
            Assert.AreEqual(TimeSpan.FromSeconds(6), policy.GetDelay(2));
            Assert.AreEqual(TimeSpan.FromSeconds(8), policy.GetDelay(3));
            Assert.AreEqual(TimeSpan.FromSeconds(10), policy.GetDelay(4));
            Assert.AreEqual(TimeSpan.FromSeconds(12), policy.GetDelay(5));
        }

        [Test]
        public void LinearBackoffDelayPolicy_BacksOffLinearly_UntilReachingMaximumDelay()
        {
            TimeSpan minimumDelay = TimeSpan.FromSeconds(2);
            TimeSpan maximumDelay = TimeSpan.FromSeconds(7);
            var policy = new LinearBackoffDelayPolicy(minimumDelay, maximumDelay);

            Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(0));
            Assert.AreEqual(TimeSpan.FromSeconds(4), policy.GetDelay(1));
            Assert.AreEqual(TimeSpan.FromSeconds(6), policy.GetDelay(2));
            Assert.AreEqual(TimeSpan.FromSeconds(7), policy.GetDelay(3));
            Assert.AreEqual(TimeSpan.FromSeconds(7), policy.GetDelay(4));
            Assert.AreEqual(TimeSpan.FromSeconds(7), policy.GetDelay(5));
        }

        #endregion

        #region ExponentialBackoffDelayPolicy

        [Test]
        public void ExponentialBackoffDelayPolicy_BacksOffExponentially()
        {
            TimeSpan minimumDelay = TimeSpan.FromSeconds(2);
            var policy = new ExponentialBackoffDelayPolicy(minimumDelay);

            Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(0));
            Assert.AreEqual(TimeSpan.FromSeconds(4), policy.GetDelay(1));
            Assert.AreEqual(TimeSpan.FromSeconds(8), policy.GetDelay(2));
            Assert.AreEqual(TimeSpan.FromSeconds(16), policy.GetDelay(3));
            Assert.AreEqual(TimeSpan.FromSeconds(32), policy.GetDelay(4));
            Assert.AreEqual(TimeSpan.FromSeconds(64), policy.GetDelay(5));
            Assert.AreEqual(TimeSpan.FromSeconds(128), policy.GetDelay(6));
        }

        [Test]
        public void ExponentialBackoffDelayPolicy_BacksOffExponentially_UntilReachingMaximumDelay()
        {
            TimeSpan minimumDelay = TimeSpan.FromSeconds(2);
            TimeSpan maximumDelay = TimeSpan.FromSeconds(20);
            var policy = new ExponentialBackoffDelayPolicy(minimumDelay, maximumDelay);

            Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(0));
            Assert.AreEqual(TimeSpan.FromSeconds(4), policy.GetDelay(1));
            Assert.AreEqual(TimeSpan.FromSeconds(8), policy.GetDelay(2));
            Assert.AreEqual(TimeSpan.FromSeconds(16), policy.GetDelay(3));
            Assert.AreEqual(TimeSpan.FromSeconds(20), policy.GetDelay(4));
            Assert.AreEqual(TimeSpan.FromSeconds(20), policy.GetDelay(5));
            Assert.AreEqual(TimeSpan.FromSeconds(20), policy.GetDelay(6));
        }

        #endregion
    }
}
