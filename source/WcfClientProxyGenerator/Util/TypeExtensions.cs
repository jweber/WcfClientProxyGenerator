using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator.Util
{
    internal static class TypeExtensions
    {
        public static IEnumerable<Type> GetAllInheritedTypes(
            this Type @type, 
            bool includeInterfaces = true)
        {
            var types = new HashSet<Type>();
            while (@type != null)
            {
                types.Add(@type);
                
                if (includeInterfaces)
                {
                    foreach (var interfaceType in @type.GetInterfaces())
                        types.Add(interfaceType);
                }

                @type = @type.BaseType;
            }

            return types;
        }
    }
}
