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
        public void Test()
        {
            var inst = FastActivator.CreateInstance<TestClass>();
            var inst2 = FastActivator.CreateInstance<TestClass>("test1");
            var inst3 = FastActivator.CreateInstance<TestClass>("test2", true, DateTime.Now);
        }

        [Test]
        public void Benchmark()
        {
            Create("Activator", () => (TestClass) Activator.CreateInstance(typeof(TestClass), "test", true, DateTime.Now));
            Create("FastActivator", () => FastActivator.CreateInstance<TestClass>("test", true, DateTime.Now));
            
            Create("Activator (parameterless)", () => (TestClass) Activator.CreateInstance(typeof(TestClass)));
            Create("FastActivator (parameterless)", FastActivator.CreateInstance<TestClass>);
        }

        private void Create(string method, Func<TestClass> func)
        {
            const int iterations = 1000000;

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var instance = func();
            }
            Trace.WriteLine(string.Format("{0}: {1}", method, stopwatch.Elapsed));
            
        }

        class TestClass
        {
            private readonly string _arg1;
            private readonly bool _arg2;
            private readonly DateTime _arg3;

            public TestClass()
            {}

            public TestClass(string arg1)
            {
                _arg1 = arg1;
            }

            public TestClass(string arg1, bool arg2, DateTime arg3)
            {
                _arg1 = arg1;
                _arg2 = arg2;
                _arg3 = arg3;
            }
        }
    }
}
