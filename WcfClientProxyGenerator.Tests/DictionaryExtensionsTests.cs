using System;
using System.Collections.Concurrent;
using System.Threading;

using Shouldly;
using WcfClientProxyGenerator.Util;
using Xunit;

namespace WcfClientProxyGenerator.Tests
{
    public class DictionaryExtensionsTests
    {
        [Fact]
        public void Safe_ConcurrentDictionary_GetOrAdd_CallsValueFactoryOnlyOnce()
        {
            var dictionary = new ConcurrentDictionary<string, Lazy<string>>();
            string thread1Message = null, thread2Message = null;

            var thread1 = new Thread(() => 
                dictionary.GetOrAddSafe("key", _ =>
                {
                    Thread.SpinWait(10000);
                    thread1Message = "thread 1";
                    return thread1Message;
                }));

            var thread2 = new Thread(() => 
                dictionary.GetOrAddSafe("key", _ =>
                {
                    Thread.SpinWait(10000);
                    thread2Message = "thread 2";
                    return thread2Message;
                }));

            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();

            bool thread1AndNotThread2 = thread1Message == "thread 1" && thread2Message == null;
            bool thread2AndNotThread1 = thread2Message == "thread 2" && thread1Message == null;

            (thread1AndNotThread2 || thread2AndNotThread1).ShouldBeTrue();
        }
    }
}