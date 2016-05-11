using System;
using System.Threading;
using NUnit.Framework;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public static class ResetEventExt
    {
        public static void WaitOrFail(this AutoResetEvent resetEvent, string message)
        {
            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(1)))
                Assert.Fail(message);
        }
    }
}