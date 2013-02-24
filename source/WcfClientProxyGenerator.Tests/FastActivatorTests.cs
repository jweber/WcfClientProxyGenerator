using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class FastActivatorTests
    {
        [Test]
        public void Test_Parameterless()
        {
            var inst = FastActivator.CreateInstance<TestClass>();
            Assert.That(inst, Is.Not.Null);
            Assert.That(inst.Arg1, Is.EqualTo(default(string)));
            Assert.That(inst.Arg2, Is.EqualTo(default(bool)));
            Assert.That(inst.Arg3, Is.EqualTo(default(DateTime)));
        }

        [Test]
        public void Test_Parameterized()
        {
            var dateTime = DateTime.Now;
            var instance = FastActivator.CreateInstance<TestClass>(new object[] { "test2", true, dateTime });
            
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.Arg1, Is.EqualTo("test2"));
            Assert.That(instance.Arg2, Is.EqualTo(true));
            Assert.That(instance.Arg3, Is.EqualTo(dateTime));
        }

        [Test]
        public void Benchmark()
        {
            var activatorDuration = Benchmark("Activator", () => (TestClass) Activator.CreateInstance(typeof(TestClass), "test", true, DateTime.Now));
            var fastActivatorDuration = Benchmark("FastActivator", () => FastActivator.CreateInstance<TestClass>(new object[] { "test", true, DateTime.Now }));

            Assert.That(fastActivatorDuration, Is.LessThan(activatorDuration));

            Benchmark("Activator (parameterless)", () => (TestClass) Activator.CreateInstance(typeof(TestClass)));
            Benchmark("FastActivator (parameterless)", FastActivator.CreateInstance<TestClass>);
        }

        private TimeSpan Benchmark(string method, Func<TestClass> func)
        {
            const int iterations = 1000000;

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var instance = func();
            }
            stopwatch.Stop();

            Trace.WriteLine(string.Format("{0}: {1}", method, stopwatch.Elapsed));
            return stopwatch.Elapsed;
        }

        class TestClass
        {
            private readonly string _arg1;
            private readonly bool _arg2;
            private readonly DateTime _arg3;

            public TestClass()
            {}

            public TestClass(string arg1, bool arg2, DateTime arg3)
            {
                _arg1 = arg1;
                _arg2 = arg2;
                _arg3 = arg3;
            }

            public string Arg1
            {
                get { return _arg1; }
            }

            public bool Arg2
            {
                get { return _arg2; }
            }

            public DateTime Arg3
            {
                get { return _arg3; }
            }
        }
    }
}
