using System;
using System.Linq;
using NUnit.Framework;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class TypeExtensionsTests
    {
        [Test]
        public void GetAllInheritedTypes_ForObjectType_ReturnsObjectType()
        {
            var types = typeof(object).GetAllInheritedTypes();

            Assert.That(types.Count(), Is.EqualTo(1));
            Assert.That(types, Contains.Item(typeof(object)));
        }

        [Test]
        public void GetAllInheritedTypes_ForValueType()
        {
            var types = typeof(int).GetAllInheritedTypes();

            Assert.That(types, Contains.Item(typeof(int)));
            Assert.That(types, Contains.Item(typeof(ValueType)));
            Assert.That(types, Contains.Item(typeof(object)));
        }

        [Test]
        public void GetAllInheritedTypes_ForCustomType_ReturnsExpectedTypes()
        {
            var types = typeof(TestClass).GetAllInheritedTypes();

            Assert.That(types.Count(), Is.EqualTo(4));

            Assert.That(types, Contains.Item(typeof(TestClass)));
            Assert.That(types, Contains.Item(typeof(ITestInterface)));
            Assert.That(types, Contains.Item(typeof(IDisposable)));
            Assert.That(types, Contains.Item(typeof(object)));
        }
        
        [Test]
        public void GetAllInheritedTypes_ForCustomType_WithoutInterfaces_ReturnsExpectedTypes()
        {
            var types = typeof(TestClass).GetAllInheritedTypes(includeInterfaces: false);

            Assert.That(types.Count(), Is.EqualTo(2));

            Assert.That(types, Contains.Item(typeof(TestClass)));
            Assert.That(types, Contains.Item(typeof(object)));
        }

        [Test]
        public void GetAllInheritedTypes_ForChildCustomType_ReturnsExpectedTypes()
        {
            var types = typeof(ChildTestClass).GetAllInheritedTypes();

            Assert.That(types.Count(), Is.EqualTo(5));

            Assert.That(types, Contains.Item(typeof(ChildTestClass)));
            Assert.That(types, Contains.Item(typeof(TestClass)));
            Assert.That(types, Contains.Item(typeof(ITestInterface)));
            Assert.That(types, Contains.Item(typeof(IDisposable)));
            Assert.That(types, Contains.Item(typeof(object)));
        }

        interface ITestInterface : IDisposable
        {}

        class TestClass : ITestInterface
        {
            public void Dispose()
            {}
        }

        class ChildTestClass : TestClass
        {
        }
    }
}
