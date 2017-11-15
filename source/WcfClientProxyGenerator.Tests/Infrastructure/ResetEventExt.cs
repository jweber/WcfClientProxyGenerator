using System;
using System.Threading;
using Xunit;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public static class ResetEventExt
    {
        public static void WaitOrFail(this AutoResetEvent resetEvent, string message)
        {
            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(1)))
                Assert.True(false, message);
        }
    }
}