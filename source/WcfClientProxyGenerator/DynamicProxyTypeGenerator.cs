using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    internal static class DynamicProxyAssembly
    {
        static DynamicProxyAssembly()
        {
            var assemblyName = new AssemblyName("WcfClientProxyGenerator.DynamicProxy");
            var appDomain = System.Threading.Thread.GetDomain();

#if OUTPUT_PROXY_DLL
            AssemblyBuilder = appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            ModuleBuilder = AssemblyBuilder.DefineDynamicModule(assemblyName.Name, "WcfClientProxyGenerator.DynamicProxy.dll");
#else
            AssemblyBuilder = appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder = AssemblyBuilder.DefineDynamicModule(assemblyName.Name);
#endif
        }

        public static AssemblyBuilder AssemblyBuilder { get; private set; }
        public static ModuleBuilder ModuleBuilder { get; private set; }
    }

    internal static class DynamicProxyTypeGenerator<TServiceInterface>
        where TServiceInterface : class
    {
        public static Type GenerateType()
        {
            var moduleBuilder = DynamicProxyAssembly.ModuleBuilder;

            var typeBuilder = moduleBuilder.DefineType(
                "-proxy-" + typeof(TServiceInterface).Name,
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(RetryingWcfActionInvokerProvider<TServiceInterface>));
            
            typeBuilder.AddInterfaceImplementation(typeof(TServiceInterface));

            SetDebuggerDisplay(typeBuilder, typeof(TServiceInterface).Name + " (wcf proxy)");

//            GenerateTypeConstructor(typeBuilder, typeof(string));
//            GenerateTypeConstructor(typeBuilder, typeof(Binding), typeof(EndpointAddress));

            var interfaceTypeHierarchy = typeof(TServiceInterface)
                .GetAllInheritedTypes(includeInterfaces: true)
                .Where(t => t.IsInterface);

            var serviceMethods = interfaceTypeHierarchy
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(t => t.GetCustomAttribute<OperationContractAttribute>() != null);

            foreach (var serviceMethod in serviceMethods)
            {
                GenerateServiceProxyMethod(serviceMethod, moduleBuilder, typeBuilder);
            }

            Type generatedType = typeBuilder.CreateType();

#if OUTPUT_PROXY_DLL
            DynamicProxyAssembly.AssemblyBuilder.Save("WcfClientProxyGenerator.DynamicProxy.dll");
#endif

            return generatedType;
        }

        private static void SetDebuggerDisplay(TypeBuilder typeBuilder, string display)
        {
            var attributCtor = typeof(DebuggerDisplayAttribute).GetConstructor(new[] { typeof(string) });
            var attributeBuilder = new CustomAttributeBuilder(attributCtor, new object[] { display });
            typeBuilder.SetCustomAttribute(attributeBuilder);
        }

        private static void GenerateTypeConstructor(TypeBuilder typeBuilder, params Type[] argumentParameterTypes)
        {
            var constructorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public, 
                CallingConventions.Standard, 
                argumentParameterTypes);

            var ilGenerator = constructorBuilder.GetILGenerator();

            ilGenerator.Emit(OpCodes.Ldarg_0); // this

            for (int i = 0; i < argumentParameterTypes.Length; i++)
                ilGenerator.Emit(OpCodes.Ldarg, (i + 1));

            var baseCtor = typeof(RetryingWcfActionInvokerProvider<TServiceInterface>)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, argumentParameterTypes, null);

            ilGenerator.Emit(OpCodes.Call, baseCtor);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateServiceProxyMethod(
            MethodInfo methodInfo, 
            ModuleBuilder moduleBuilder, 
            TypeBuilder typeBuilder)
        {
            var parameterTypes = methodInfo.GetParameters()
                .Select(m => m.ParameterType)
                .ToArray();

            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                methodInfo.ReturnType,
                parameterTypes);

            Type serviceCallWrapperType;
            var serviceCallWrapperFields = GenerateServiceCallWrapperType(
                methodInfo, 
                moduleBuilder, 
                parameterTypes, 
                out serviceCallWrapperType);

            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.DeclareLocal(typeof(RetryingWcfActionInvoker<TServiceInterface>));

            ilGenerator.DeclareLocal(methodInfo.ReturnType == typeof(void)
                ? typeof(Action<>).MakeGenericType(typeof(TServiceInterface))
                : typeof(Func<,>).MakeGenericType(typeof(TServiceInterface), methodInfo.ReturnType));

            ilGenerator.DeclareLocal(serviceCallWrapperType);
            
            if (methodInfo.ReturnType != typeof(void))
                ilGenerator.DeclareLocal(methodInfo.ReturnType);

            var serviceCallWrapperCtor = serviceCallWrapperType.GetConstructor(Type.EmptyTypes);
            if (serviceCallWrapperCtor == null)
                throw new Exception("Parameterless constructor not found for type: " + serviceCallWrapperType);

            ilGenerator.Emit(OpCodes.Newobj, serviceCallWrapperCtor);
            ilGenerator.Emit(OpCodes.Stloc_2);
            ilGenerator.Emit(OpCodes.Ldloc_2);
            
            for (int i = 0; i < serviceCallWrapperFields.Count; i++)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i + 1);
                ilGenerator.Emit(OpCodes.Stfld, serviceCallWrapperType.GetField(serviceCallWrapperFields[i].Name));

                if (i < serviceCallWrapperFields.Count)
                    ilGenerator.Emit(OpCodes.Ldloc_2);
            }

            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            
            var channelProperty = typeof(RetryingWcfActionInvokerProvider<TServiceInterface>)
                .GetMethod(
                    "get_ActionInvoker", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);

            ilGenerator.Emit(OpCodes.Call, channelProperty);
            ilGenerator.Emit(OpCodes.Stloc_0);
            ilGenerator.Emit(OpCodes.Ldloc_2);

            var serviceCallWrapperGetMethod = serviceCallWrapperType
                .GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
            
            ilGenerator.Emit(OpCodes.Ldftn, serviceCallWrapperGetMethod);
            
            ConstructorInfo ctor = GetDelegateConstructor(methodInfo);
            
            ilGenerator.Emit(OpCodes.Newobj, ctor);
            ilGenerator.Emit(OpCodes.Stloc_1);
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Ldloc_1);

            MethodInfo invokeMethod = GetRetryingActionInvokerMethod(methodInfo);

            ilGenerator.Emit(OpCodes.Callvirt, invokeMethod);

            if (methodInfo.ReturnType != typeof(void))
            {
                ilGenerator.Emit(OpCodes.Stloc_3);
                ilGenerator.Emit(OpCodes.Ldloc_3);
            }

            ilGenerator.Emit(OpCodes.Ret);
        }

        private static ConstructorInfo GetDelegateConstructor(MethodInfo methodInfo)
        {
            if (methodInfo.ReturnType == typeof(void))
            {
                return typeof(Action<>)
                    .MakeGenericType(typeof(TServiceInterface))
                    .GetConstructor(new Type[] { typeof(object), typeof(IntPtr) });
            }

            return typeof(Func<,>)
                .MakeGenericType(typeof(TServiceInterface), methodInfo.ReturnType)
                .GetConstructor(new Type[] { typeof(object), typeof(IntPtr) });
        }

        private static MethodInfo GetRetryingActionInvokerMethod(MethodInfo methodInfo)
        {
            if (methodInfo.ReturnType == typeof(void))
            {
                Type actionType = typeof(Action<>)
                    .MakeGenericType(typeof(TServiceInterface));

                return typeof(RetryingWcfActionInvoker<TServiceInterface>)
                    .GetMethod("Invoke", new[] { actionType });
            }

            var funcInvokeMethod = typeof(RetryingWcfActionInvoker<TServiceInterface>)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(m => m.Name == "Invoke" && m.ReturnType != typeof(void));

            return funcInvokeMethod.MakeGenericMethod(new[] { methodInfo.ReturnType });

        }

        /// <summary>
        /// Builds the type used by the call to the <see cref="IActionInvoker{TServiceInterface}.Invoke{TResponse}"/>
        /// method.
        /// </summary>
        private static IList<FieldBuilder> GenerateServiceCallWrapperType(
            MethodInfo methodInfo, 
            ModuleBuilder moduleBuilder, 
            Type[] parameterTypes, 
            out Type generatedType)
        {
            string typeName = string.Format(
                "-call-{0}.{1}",
                typeof(TServiceInterface).Name,
                methodInfo.Name);

            var serviceCallTypeBuilder = moduleBuilder.DefineType(typeName);

            var fields = new List<FieldBuilder>(parameterTypes.Length);
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                Type parameterType = parameterTypes[i];
                fields.Add(
                    serviceCallTypeBuilder.DefineField("arg" + i, parameterType, FieldAttributes.Public));
            }

            var methodBuilder = serviceCallTypeBuilder.DefineMethod(
                "Get",
                MethodAttributes.Public,
                methodInfo.ReturnType,
                new[] { typeof(TServiceInterface) });

            var ilGenerator = methodBuilder.GetILGenerator();
            
            if (methodInfo.ReturnType != typeof(void))
                ilGenerator.DeclareLocal(methodInfo.ReturnType);
            
            ilGenerator.Emit(OpCodes.Ldarg_1);

            fields.ForEach(lf =>
            {
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, lf);
            });

            ilGenerator.Emit(OpCodes.Callvirt, methodInfo);
            
            if (methodInfo.ReturnType != typeof(void))
            {
                ilGenerator.Emit(OpCodes.Stloc_0);
                ilGenerator.Emit(OpCodes.Ldloc_0);
            }

            ilGenerator.Emit(OpCodes.Ret);

            generatedType = serviceCallTypeBuilder.CreateType();
            return fields;
        }
    }
}
