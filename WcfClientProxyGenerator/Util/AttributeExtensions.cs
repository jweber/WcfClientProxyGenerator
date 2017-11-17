using System;
using System.Linq;
using System.Reflection;

namespace WcfClientProxyGenerator.Util
{
    internal static class AttributeExtensions
    {
        public static bool HasAttribute<TAttribute>(this ICustomAttributeProvider provider)
            where TAttribute : Attribute
        {
            return provider.GetCustomAttributes(typeof(TAttribute), false).Any();
        }
    }
}
