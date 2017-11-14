using System;
using System.Diagnostics;
using Shouldly;
using WcfClientProxyGenerator.Util;
using Xunit;

namespace WcfClientProxyGenerator.Tests
{
    public class FastActivatorTests
    {
        public FastActivatorTests()
        {
            FastActivator.ClearActivatorCache();
        }

        [Fact]
        public void GenericType_CanBeActivated_UsingParameterlessConstructor()
        {
            var inst = FastActivator.CreateInstance<TestClass>();
            inst.ShouldNotBeNull();
            inst.Arg1.ShouldBe(default(string));
            inst.Arg2.ShouldBe(default(bool));
            inst.Arg3.ShouldBe(default(DateTime));
        }

        [Fact]
        public void GenericType_CanBeActivated_UsingParameterizedConstructor()
        {
            var dateTime = DateTime.Now;
            var instance = FastActivator.CreateInstance<TestClass>(new object[] { "test2", true, dateTime });
            
            instance.ShouldNotBeNull();
            instance.Arg1.ShouldBe("test2");
            instance.Arg2.ShouldBe(true);
            instance.Arg3.ShouldBe(dateTime);
        }

        [Fact]
        public void Type_CanBeActivated_UsingParameterlessConstructor()
        {
            var inst = FastActivator.CreateInstance(typeof(TestClass)) as TestClass;

            inst.ShouldNotBeNull();
            inst.Arg1.ShouldBe(default(string));
            inst.Arg2.ShouldBe(default(bool));
            inst.Arg3.ShouldBe(default(DateTime));
        }

        [Fact]
        public void Type_CanBeActivated_UsingParameterizedConstructor()
        {
            var dateTime = DateTime.Now;
            var instance = FastActivator.CreateInstance(typeof(TestClass), new object[] { "test2", true, dateTime }) as TestClass;
            
            instance.ShouldNotBeNull();
            instance.Arg1.ShouldBe("test2");
            instance.Arg2.ShouldBe(true);
            instance.Arg3.ShouldBe(dateTime);
        }

        [Fact]
        public void MultipleGenericTypes_CanBeActivated_UsingParameterlessConstructor()
        {
            var instance1 = FastActivator.CreateInstance<TestClass>();
            var instance2 = FastActivator.CreateInstance<TestClass2>();

            instance1.ShouldNotBeNull();
            instance1.GetType().ShouldBe(typeof(TestClass));
            
            instance2.ShouldNotBeNull();
            instance2.GetType().ShouldBe(typeof(TestClass2));
        }

        [Fact]
        public void MultipleGenericTypes_CanBeActivated_UsingParameterizedConstructor()
        {
            var instance1 = FastActivator.CreateInstance<TestClass>(new object[] { "instance1" });
            var instance2 = FastActivator.CreateInstance<TestClass2>(new object[] { "instance2" });

            instance1.ShouldNotBeNull();
            instance1.Arg1.ShouldBe("instance1");
            instance1.GetType().ShouldBe(typeof(TestClass));
            
            instance2.ShouldNotBeNull();
            instance2.Arg1.ShouldBe("instance2");
            instance2.GetType().ShouldBe(typeof(TestClass2));
        }

        [Fact]
        public void MultipleTypes_CanBeActivated_UsingParameterlessConstructor()
        {
            var instance1 = FastActivator.CreateInstance(typeof(TestClass));
            var instance2 = FastActivator.CreateInstance(typeof(TestClass2));

            instance1.ShouldNotBeNull();
            instance1.GetType().ShouldBe(typeof(TestClass));
            
            instance2.ShouldNotBeNull();
            instance2.GetType().ShouldBe(typeof(TestClass2));
        }

        [Fact]
        public void MultipleTypes_CanBeActivated_UsingParameterizedConstructor()
        {
            var instance1 = FastActivator.CreateInstance(typeof(TestClass), new object[] { "instance1" }) as TestClass;
            var instance2 = FastActivator.CreateInstance(typeof(TestClass2), new object[] { "instance2" }) as TestClass2;

            instance1.ShouldNotBeNull();
            instance1.Arg1.ShouldBe("instance1");
            
            instance2.ShouldNotBeNull();
            instance2.Arg1.ShouldBe("instance2");
        }

        [Fact]
        public void Benchmark()
        {
            var activatorDuration = RunBenchmark("Activator", () => (TestClass) Activator.CreateInstance(typeof(TestClass), "test", true, DateTime.Now));
            var fastActivatorDuration = RunBenchmark("FastActivator", () => FastActivator.CreateInstance<TestClass>(new object[] { "test", true, DateTime.Now }));

            fastActivatorDuration.ShouldBeLessThan(activatorDuration);

            RunBenchmark("Activator (parameterless)", () => (TestClass) Activator.CreateInstance(typeof(TestClass)));
            RunBenchmark("FastActivator (parameterless)", FastActivator.CreateInstance<TestClass>);
        }

        private TimeSpan RunBenchmark(string method, Func<TestClass> func)
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

        class TestClass2
        {
            private readonly string _arg1;

            public TestClass2()
            {}

            public TestClass2(string arg1)
            {
                _arg1 = arg1;
            }

            public string Arg1
            {
                get { return _arg1; }
            }
        }
    }
}
