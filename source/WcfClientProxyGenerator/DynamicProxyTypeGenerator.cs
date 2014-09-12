#define OUTPUT_PROXY_DLL

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel;
using System.Threading.Tasks;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator
{
    /// <summary>
    /// Static class to hold the cached instance of the dynamic assembly
    /// </summary>
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

    /// <summary>
    /// Dynamic type generator for WCF interfaces. Builds an implementation
    /// of <typeparamref name="TServiceInterface"/> at runtime that passes calls
    /// through to the <see cref="IActionInvoker{TServiceInterface}"/>
    /// </summary>
    /// <typeparam name="TServiceInterface">
    /// WCF based interface that is decorated with the <c>System.ServiceModel</c> attributes
    /// </typeparam>
    internal static class DynamicProxyTypeGenerator<TServiceInterface>
        where TServiceInterface : class
    {
        public static Type GenerateType(Type actionInvokerProviderType)
        {
            CheckServiceInterfaceValidity(typeof(TServiceInterface));

            var moduleBuilder = DynamicProxyAssembly.ModuleBuilder;

            var interfaceTypeHierarchy = typeof(TServiceInterface)
                .GetAllInheritedTypes(includeInterfaces: true)
                .Where(t => t.IsInterface);

            var serviceMethods = interfaceTypeHierarchy
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(t => t.HasAttribute<OperationContractAttribute>());

            if (!serviceMethods.Any())
            {
                throw new InvalidOperationException(String.Format("Service interface {0} has no OperationContact methods. Is this a proper WCF service interface?", typeof(TServiceInterface).Name));
            }

            var asyncInterfaceBuilder = moduleBuilder.DefineType(
                "WcfClientProxyGenerator.DynamicProxy." + typeof(TServiceInterface).Name + "Async",
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);

            asyncInterfaceBuilder.AddInterfaceImplementation(typeof(TServiceInterface));

            var attributeCtor = typeof(ServiceContractAttribute)
                .GetConstructor(Type.EmptyTypes);

            var attributeBuilder = new CustomAttributeBuilder(attributeCtor, new object[0]);
            asyncInterfaceBuilder.SetCustomAttribute(attributeBuilder);

            foreach (var serviceMethod in serviceMethods)
                GenerateAsyncTaskMethod(serviceMethod, asyncInterfaceBuilder);

            Type asyncInterface = asyncInterfaceBuilder.CreateType();

            // build proxy

            var genericActionInvokerType = actionInvokerProviderType
                .MakeGenericType(asyncInterface);

            var typeBuilder = moduleBuilder.DefineType(
                "WcfClientProxyGenerator.DynamicProxy." + typeof(TServiceInterface).Name,
                TypeAttributes.Public | TypeAttributes.Class,
                genericActionInvokerType);
            
            //typeBuilder.AddInterfaceImplementation(typeof(TServiceInterface));
            typeBuilder.AddInterfaceImplementation(asyncInterface);

            SetDebuggerDisplay(typeBuilder, typeof(TServiceInterface).Name + " (wcf proxy)");
            
            interfaceTypeHierarchy = asyncInterface
                .GetAllInheritedTypes(includeInterfaces: true)
                .Where(t => t.IsInterface);

            serviceMethods = interfaceTypeHierarchy
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(t => t.HasAttribute<OperationContractAttribute>());

            foreach (var serviceMethod in serviceMethods)
            {
                GenerateServiceProxyMethod(asyncInterface, serviceMethod, typeBuilder);
            }

            Type generatedType = typeBuilder.CreateType();
            
#if OUTPUT_PROXY_DLL 
            DynamicProxyAssembly.AssemblyBuilder.Save("WcfClientProxyGenerator.DynamicProxy.dll");
#endif

            return generatedType;
        }

        private static void CheckServiceInterfaceValidity(Type type)
        {
            if (!type.IsPublic && !type.IsNestedPublic)
            {
                throw new InvalidOperationException(String.Format("Service interface {0} is not declared public. WcfClientProxyGenerator cannot work with non-public service interfaces.", type.Name));
            }

            if (!type.HasAttribute<ServiceContractAttribute>())
            {
                throw new InvalidOperationException(String.Format("Service interface {0} is not marked with ServiceContract attribute. Is this a proper WCF service interface?", type.Name));
            }
        }

        private static void SetDebuggerDisplay(TypeBuilder typeBuilder, string display)
        {
            var attributeCtor = typeof(DebuggerDisplayAttribute)
                .GetConstructor(new[] { typeof(string) });
           
            if (attributeCtor == null)
                throw new NotImplementedException("No constructor found on type 'DebuggerDisplayAttribute' that takes an argument of 'string'");

            var attributeBuilder = new CustomAttributeBuilder(attributeCtor, new object[] { display });
            typeBuilder.SetCustomAttribute(attributeBuilder);
        }

        private static void GenerateAsyncTaskMethod(
            MethodInfo methodInfo,
            TypeBuilder typeBuilder)
        {
            var parameterTypes = methodInfo.GetParameters()
                .Select(m => m.ParameterType)
                .ToArray();

            Type returnType = methodInfo.ReturnType;
            if (returnType == typeof(void))
            {
                returnType = typeof(Task);
            }
            else
            {
                returnType = typeof(Task<>).MakeGenericType(returnType);
            }

            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name + "Async",
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.Abstract,
                returnType,
                parameterTypes);

            for (int i = 1; i <= parameterTypes.Length; i++)
                methodBuilder.DefineParameter(i, ParameterAttributes.None, "arg" + i);

            var originalOperationContract = methodInfo.GetCustomAttribute<OperationContractAttribute>();

            var attributeCtor = typeof(OperationContractAttribute)
                .GetConstructor(Type.EmptyTypes);

            var actionProp = typeof(OperationContractAttribute)
                .GetProperty("Action");

            var replyActionProp = typeof(OperationContractAttribute)
                .GetProperty("ReplyAction");

            var attributeBuilder = new CustomAttributeBuilder(
                attributeCtor, 
                new object[0], 
                new [] { actionProp, replyActionProp }, 
                new object[] { originalOperationContract.Action, originalOperationContract.ReplyAction });

            methodBuilder.SetCustomAttribute(attributeBuilder);
        }

        /// <summary>
        /// Generates the methods on the <paramref name="typeBuilder">dynamic type</paramref> 
        /// to satisfy the <see cref="OperationContractAttribute"/> interface contracts.
        /// </summary>
        /// <param name="asyncInterfaceType"></param>
        /// <param name="methodInfo">MethodInfo from the interface</param>
        /// <param name="typeBuilder">The dynamic type</param>
        private static void GenerateServiceProxyMethod(
            Type asyncInterfaceType,
            MethodInfo methodInfo, 
            TypeBuilder typeBuilder)
        {
            var parameterTypes = methodInfo.GetParameters()
                .Select(m => m.ParameterType)
                .ToArray();

            // TReturn Method(TParamType1 arg1, TParamType2 arg2, ...) {
            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                methodInfo.ReturnType,
                parameterTypes);

            for (int i = 1; i <= parameterTypes.Length; i++)
                methodBuilder.DefineParameter(i, ParameterAttributes.None, "arg" + i);

            Type serviceCallWrapperType;
            var serviceCallWrapperFields = GenerateServiceCallWrapperType(
                methodInfo, 
                parameterTypes, 
                out serviceCallWrapperType);

            FieldBuilder[] inputFields = serviceCallWrapperFields.Where(f => f.Name.StartsWith("arg")).ToArray();
            FieldBuilder[] outputFields = serviceCallWrapperFields.Where(f => f.Name.StartsWith("out")).ToArray();

            var ilGenerator = methodBuilder.GetILGenerator();

            // IActionInvoker<TServiceInterface> local0;
            ilGenerator.DeclareLocal(typeof(IActionInvoker<>).MakeGenericType(asyncInterfaceType));

            // (void methods) Action<TServiceInterface> local1;
            // (methods with return value) Func<TServiceInterface, TReturnType> local1;
            ilGenerator.DeclareLocal(methodInfo.ReturnType == typeof(void)
                ? typeof(Action<>).MakeGenericType(asyncInterfaceType)
                : typeof(Func<,>).MakeGenericType(asyncInterfaceType, methodInfo.ReturnType));

            // MethodType local2;
            ilGenerator.DeclareLocal(serviceCallWrapperType);
            
            // local variable to store result to
            // TResult local3;
            if (methodInfo.ReturnType != typeof(void))
                ilGenerator.DeclareLocal(methodInfo.ReturnType);
            else
                ilGenerator.DeclareLocal(typeof(bool)); // generate unused variable to make referencing local variables easier

            // local variable to store invocation information (which method and parameters)
            // InvokeInfo local4;
            ilGenerator.DeclareLocal(typeof(InvokeInfo));

            // local variable to store parameter information
            // object[] local5;
            ilGenerator.DeclareLocal(typeof(object[]));

            var serviceCallWrapperCtor = serviceCallWrapperType.GetConstructor(Type.EmptyTypes);
            if (serviceCallWrapperCtor == null)
                throw new Exception("Parameterless constructor not found for type: " + serviceCallWrapperType);

            // local2 = new MethodType();
            ilGenerator.Emit(OpCodes.Newobj, serviceCallWrapperCtor);
            ilGenerator.Emit(OpCodes.Stloc_2);
            ilGenerator.Emit(OpCodes.Ldloc_2);
            
            for (int i = 0; i < serviceCallWrapperFields.Count; i++)
            {
                FieldBuilder field = serviceCallWrapperFields[i];
                if (!inputFields.Contains(field))
                    continue;

                ilGenerator.Emit(OpCodes.Ldarg, i + 1);
                ilGenerator.Emit(OpCodes.Stfld, serviceCallWrapperType.GetField(field.Name));

                if (i < serviceCallWrapperFields.Count)
                    ilGenerator.Emit(OpCodes.Ldloc_2);
            }

            // arg0 is RetryingWcfActionInvokerProvider<TServiceInterface>
            // local0 = arg0.ActionInvoker;
            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            
            var channelProperty = typeof(RetryingWcfActionInvokerProvider<>)
                .MakeGenericType(asyncInterfaceType)
                .GetMethod(
                    "get_ActionInvoker", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);

            ilGenerator.Emit(OpCodes.Call, channelProperty);
            ilGenerator.Emit(OpCodes.Stloc_0);
            
            // create method that is called by the Invoke() in RetryingWcfActionInvoker
            // local1 = new Action<TServiceInterface>(service => wrappermethod(service));
            var serviceCallWrapperGetMethod = serviceCallWrapperType
                .GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
            
            ilGenerator.Emit(OpCodes.Ldftn, serviceCallWrapperGetMethod);
            
            ConstructorInfo ctor = GetDelegateConstructor(methodInfo);
            
            ilGenerator.Emit(OpCodes.Newobj, ctor);
            ilGenerator.Emit(OpCodes.Stloc_1);

            // create InvokeInfo structure to hold data
            ilGenerator.Emit(OpCodes.Newobj, typeof(InvokeInfo).GetConstructor(Type.EmptyTypes));
            ilGenerator.Emit(OpCodes.Stloc, 4);
            ilGenerator.Emit(OpCodes.Ldloc, 4);
            ilGenerator.Emit(OpCodes.Ldstr, methodInfo.Name);
            ilGenerator.Emit(OpCodes.Callvirt, typeof(InvokeInfo).GetMethod("set_MethodName"));

            // store parameters used to InvokeInfo.Parameters structure
            ilGenerator.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
            ilGenerator.Emit(OpCodes.Newarr, typeof(object));
            ilGenerator.Emit(OpCodes.Stloc, 5);
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                ilGenerator.Emit(OpCodes.Ldloc, 5); // object[], parameters
                ilGenerator.Emit(OpCodes.Ldc_I4, i);
                ilGenerator.Emit(OpCodes.Ldarg, i + 1);
                if (parameterTypes[i].IsValueType)
                {
                    ilGenerator.Emit(OpCodes.Box, parameterTypes[i]);
                }
                ilGenerator.Emit(OpCodes.Stelem_Ref);
            }
            ilGenerator.Emit(OpCodes.Ldloc, 4); // InvokeInfo
            ilGenerator.Emit(OpCodes.Ldloc, 5); // object[] parameters
            ilGenerator.Emit(OpCodes.Callvirt, typeof(InvokeInfo).GetMethod("set_Parameters"));

            // call correct Invoke() ActionInvoker.Invoke() with parameters local1 (method to execute) and InvokeInfo
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Ldloc_1);
            ilGenerator.Emit(OpCodes.Ldloc, 4);
            MethodInfo invokeMethod = GetIActionInvokerInvokeMethod(methodInfo);

            ilGenerator.Emit(OpCodes.Callvirt, invokeMethod);

            if (outputFields.Length > 0)
            {
                ilGenerator.Emit(OpCodes.Stloc_3);
                
                for (int i = 0; i < serviceCallWrapperFields.Count; i++)
                {
                    FieldBuilder field = serviceCallWrapperFields[i];
                    if (!outputFields.Contains(field))
                        continue;

                    ilGenerator.Emit(OpCodes.Ldarg, i + 1);
                    ilGenerator.Emit(OpCodes.Ldloc_2);
                    ilGenerator.Emit(OpCodes.Ldfld, serviceCallWrapperFields[i]);
                    ilGenerator.Emit(OpCodes.Stind_Ref);
                }

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

        private static MethodInfo GetIActionInvokerInvokeMethod(MethodInfo methodInfo)
        {
            if (typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
            {
                var funcInvokeAsyncMethod = (typeof(IActionInvoker<TServiceInterface>))
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .First(m => m.Name == "InvokeAsync" && m.ReturnType != typeof(Task));

                var taskReturnType = methodInfo.ReturnType.GenericTypeArguments[0];

                return funcInvokeAsyncMethod.MakeGenericMethod(new[] { taskReturnType });
            }

            if (methodInfo.ReturnType == typeof(void))
            {
                Type actionType = typeof(Action<>)
                    .MakeGenericType(typeof(TServiceInterface));

                return typeof(IActionInvoker<TServiceInterface>)
                    .GetMethod("Invoke", new[] { actionType, typeof(InvokeInfo) });
            }

            var funcInvokeMethod = typeof(IActionInvoker<TServiceInterface>)
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
            Type[] parameterTypes, 
            out Type generatedType)
        {
            Type[] byRefParameterTypes = parameterTypes
                .Where(t => t.IsByRef)
                .ToArray();

            string typeName = string.Format(
                "WcfClientProxyGenerator.DynamicProxy.{0}Support.{1}",
                typeof(TServiceInterface).Name,
                methodInfo.Name);

            var serviceCallTypeBuilder = DynamicProxyAssembly.ModuleBuilder.DefineType(typeName);

            var fields = new List<FieldBuilder>(parameterTypes.Length);

            int inputFields = 0;
            int outputFields = 0;
            foreach (Type parameterType in parameterTypes)
            {
                string fieldName;
                Type fieldType = parameterType;
                if (parameterType.IsByRef)
                {
                    fieldName = "out" + (outputFields++);
                    fieldType = parameterType.GetElementType();
                }
                else
                {
                    fieldName = "arg" + (inputFields++);
                }

                fields.Add(
                    serviceCallTypeBuilder.DefineField(fieldName, fieldType, FieldAttributes.Public));
            }

            var methodBuilder = serviceCallTypeBuilder.DefineMethod(
                "Get",
                MethodAttributes.Public,
                methodInfo.ReturnType,
                new[] { typeof(TServiceInterface) });

            methodBuilder.DefineParameter(1, ParameterAttributes.None, "service");

            var ilGenerator = methodBuilder.GetILGenerator();
            
            if (methodInfo.ReturnType != typeof(void))
                ilGenerator.DeclareLocal(methodInfo.ReturnType);
            
            ilGenerator.Emit(OpCodes.Ldarg_1);

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                bool isByRef = parameterTypes[i].IsByRef;

                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(
                    isByRef ? OpCodes.Ldflda : OpCodes.Ldfld, 
                    fields[i]);
            }

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
