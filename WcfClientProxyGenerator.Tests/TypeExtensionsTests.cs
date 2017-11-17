using System;
using System.Linq;

using Shouldly;
using WcfClientProxyGenerator.Util;
using Xunit;

namespace WcfClientProxyGenerator.Tests
{
    public class TypeExtensionsTests
    {
        [Fact]
        public void GetAllInheritedTypes_ForObjectType_ReturnsObjectType()
        {
            var types = typeof(object).GetAllInheritedTypes();

            types.Count().ShouldBe(1);
            types.ShouldContain(typeof(object));
        }

        [Fact]
        public void GetAllInheritedTypes_ForValueType()
        {
            var types = typeof(int).GetAllInheritedTypes();

            types.ShouldContain(typeof(int));
            types.ShouldContain(typeof(ValueType));
            types.ShouldContain(typeof(object));
        }

        [Fact]
        public void GetAllInheritedTypes_ForCustomType_ReturnsExpectedTypes()
        {
            var types = typeof(TestClass).GetAllInheritedTypes();

            types.Count().ShouldBe(4);

            types.ShouldContain(typeof(TestClass));
            types.ShouldContain(typeof(ITestInterface));
            types.ShouldContain(typeof(IDisposable));
            types.ShouldContain(typeof(object));
        }
        
        [Fact]
        public void GetAllInheritedTypes_ForCustomType_WithoutInterfaces_ReturnsExpectedTypes()
        {
            var types = typeof(TestClass).GetAllInheritedTypes(includeInterfaces: false);

            types.Count().ShouldBe(2);

            types.ShouldContain(typeof(TestClass));
            types.ShouldContain(typeof(object));
        }

        [Fact]
        public void GetAllInheritedTypes_ForChildCustomType_ReturnsExpectedTypes()
        {
            var types = typeof(ChildTestClass).GetAllInheritedTypes();

            types.Count().ShouldBe(5);

            types.ShouldContain(typeof(ChildTestClass));
            types.ShouldContain(typeof(TestClass));
            types.ShouldContain(typeof(ITestInterface));
            types.ShouldContain(typeof(IDisposable));
            types.ShouldContain(typeof(object));
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
