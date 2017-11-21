using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel;
using System.Threading.Tasks;
using WcfClientProxyGenerator.Async;
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
            Initialize();
        }

        public static AssemblyBuilder AssemblyBuilder { get; private set; }
        public static ModuleBuilder ModuleBuilder { get; private set; }

        internal static void Initialize()
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
    }

    internal class GeneratedTypes
    {
        public Type Proxy { get; set; }
        public Type AsyncInterface { get; set; }
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
        public static GeneratedTypes GenerateTypes<TActionInvokerProvider>()
            where TActionInvokerProvider : IActionInvokerProvider<TServiceInterface>
        {
            Type interfaceType = typeof(TServiceInterface);
            CheckServiceInterfaceValidity(interfaceType);

            var moduleBuilder = DynamicProxyAssembly.ModuleBuilder;

            var interfaceTypeHierarchy = interfaceType
                .GetAllInheritedTypes(includeInterfaces: true)
                .Where(t => t.IsInterface);

            var serviceMethods = interfaceTypeHierarchy
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(t => t.HasAttribute<OperationContractAttribute>())
                .ToList();

            if (!serviceMethods.Any())
            {
                throw new InvalidOperationException(
                    $"Service interface {interfaceType.Name} has no OperationContact methods. Is this a proper WCF service interface?");
            }

            var asyncInterfaceType = GenerateAsyncInterface(serviceMethods);

            // build proxy

            var genericActionInvokerType = typeof(TActionInvokerProvider)
                .GetGenericTypeDefinition()
                .MakeGenericType(asyncInterfaceType);

            var typeBuilder = moduleBuilder.DefineType(
                "WcfClientProxyGenerator.DynamicProxy." + interfaceType.Name,
                TypeAttributes.Public | TypeAttributes.Class,
                genericActionInvokerType);
            
            typeBuilder.AddInterfaceImplementation(asyncInterfaceType);

            SetDebuggerDisplay(typeBuilder, interfaceType.Name + " (wcf proxy)");
            
            interfaceTypeHierarchy = asyncInterfaceType
                .GetAllInheritedTypes(includeInterfaces: true)
                .Where(t => t.IsInterface);

            serviceMethods = interfaceTypeHierarchy
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(t => t.HasAttribute<OperationContractAttribute>())
                .ToList();

            foreach (var serviceMethod in serviceMethods)
            {
                GenerateServiceProxyMethod(asyncInterfaceType, serviceMethod, typeBuilder);
            }

            Type proxyType = typeBuilder.CreateType();
            
#if OUTPUT_PROXY_DLL 
            DynamicProxyAssembly.AssemblyBuilder.Save("WcfClientProxyGenerator.DynamicProxy.dll");
#endif

            return new GeneratedTypes
            {
                Proxy = proxyType,
                AsyncInterface = asyncInterfaceType
            };
        }

        private static bool NonAsyncOperationContractHasMatchingAsyncOperationContract(
            MethodInfo nonAsyncServiceMethod, 
            IList<MethodInfo> serviceMethods)
        {
            string nonAsyncActionValue = GetOperationContractAction(nonAsyncServiceMethod);
            string nonAsyncReplyActionValue = GetOperationContractReplyAction(nonAsyncServiceMethod);

            return (from m in serviceMethods
                    let actionValue = GetOperationContractAction(m)
                    let replyActionValue = GetOperationContractReplyAction(m)
                    where m.Name == nonAsyncServiceMethod.Name + "Async"
                          && typeof (Task).IsAssignableFrom(m.ReturnType)
                          && nonAsyncActionValue == actionValue
                          && nonAsyncReplyActionValue == replyActionValue
                    select m).Any();
        }

        private static string GetOperationContractAction(MethodInfo methodInfo)
        {
            var operationContractAttr = methodInfo.GetCustomAttribute<OperationContractAttribute>();
            if (operationContractAttr == null)
                throw new Exception("No OperationContract attribute when one was expected");

            string action = operationContractAttr.Action;
            if (action != null)
                return action;

            var serviceContract = typeof(TServiceInterface).GetCustomAttribute<ServiceContractAttribute>();
            string serviceNamespace = serviceContract.Namespace ?? "http://tempuri.org";
            serviceNamespace = serviceNamespace.TrimEnd('/');

            string defaultAction =
                $"{serviceNamespace}/{serviceContract.Name ?? methodInfo.DeclaringType.Name}/{operationContractAttr.Name ?? methodInfo.Name}";

            return defaultAction;
        }
        
        private static string GetOperationContractReplyAction(MethodInfo methodInfo)
        {
            var operationContractAttr = methodInfo.GetCustomAttribute<OperationContractAttribute>();
            if (operationContractAttr == null)
                throw new Exception("No OperationContract attribute when one was expected");

            string replyAction = operationContractAttr.ReplyAction;
            if (replyAction != null)
                return replyAction;

            if (!operationContractAttr.IsOneWay)
            {
                var serviceContract = typeof (TServiceInterface).GetCustomAttribute<ServiceContractAttribute>();
                string serviceNamespace = serviceContract.Namespace ?? "http://tempuri.org";
                serviceNamespace = serviceNamespace.TrimEnd('/');

                string defaultAction =
                    $"{serviceNamespace}/{serviceContract.Name ?? methodInfo.DeclaringType.Name}/{operationContractAttr.Name ?? methodInfo.Name}Response";

                return defaultAction;
            }

            return operationContractAttr.ReplyAction;
        }

        private static IEnumerable<CustomAttributeBuilder> CloneCustomAttributes(IEnumerable<CustomAttributeData> attrData)
        {
            foreach (var data in attrData)
            {
                var ctorArgs = data.ConstructorArguments
                    .Select(m => m.Value)
                    .ToArray();

                var properties = data.NamedArguments?
                    .Where(m => m.MemberInfo is PropertyInfo)
                    .Select(m => new { pi = m.MemberInfo as PropertyInfo, val = m.TypedValue.Value })
                    .ToArray();

                var fields = data.NamedArguments?
                    .Where(m => m.MemberInfo is FieldInfo)
                    .Select(m => new { fi = m.MemberInfo as FieldInfo, val = m.TypedValue.Value })
                    .ToArray();

                yield return new CustomAttributeBuilder(
                    data.Constructor,
                    ctorArgs,
                    properties?.Select(m => m.pi).ToArray() ?? new PropertyInfo[0],
                    properties?.Select(m => m.val).ToArray() ?? new object[0],
                    fields?.Select(m => m.fi).ToArray() ?? new FieldInfo[0],
                    fields?.Select(m => m.val).ToArray() ?? new object[0]);
            }
                
        }

        private static Type GenerateAsyncInterface(IList<MethodInfo> serviceMethods)
        {
            var moduleBuilder = DynamicProxyAssembly.ModuleBuilder;

            var asyncInterfaceBuilder = moduleBuilder.DefineType(
                "WcfClientProxyGenerator.DynamicProxy." + typeof(TServiceInterface).Name + "Async",
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
            
            asyncInterfaceBuilder.AddInterfaceImplementation(typeof(TServiceInterface));

            var generatedAsyncInterfaceAttrCtor = typeof(GeneratedAsyncInterfaceAttribute)
                .GetConstructor(Type.EmptyTypes);

            var generatedAsyncInterfaceAttrBuilder = new CustomAttributeBuilder(generatedAsyncInterfaceAttrCtor, new object[0]);
            asyncInterfaceBuilder.SetCustomAttribute(generatedAsyncInterfaceAttrBuilder);

            foreach (var builder in CloneCustomAttributes(typeof(TServiceInterface).GetCustomAttributesData()))
                asyncInterfaceBuilder.SetCustomAttribute(builder);

            var nonAsyncServiceMethods = serviceMethods
                .Where(m => !typeof(Task).IsAssignableFrom(m.ReturnType)
                            && !NonAsyncOperationContractHasMatchingAsyncOperationContract(m, serviceMethods));

            foreach (var serviceMethod in nonAsyncServiceMethods)
                GenerateAsyncTaskMethod(serviceMethod, asyncInterfaceBuilder);

            Type asyncInterface = asyncInterfaceBuilder.CreateType();

            return asyncInterface;
        }

        private static void CheckServiceInterfaceValidity(Type type)
        {
            if (!type.IsPublic && !type.IsNestedPublic)
            {
                throw new InvalidOperationException(
                    $"Service interface {type.Name} is not declared public. WcfClientProxyGenerator cannot work with non-public service interfaces.");
            }

            if (!type.HasAttribute<ServiceContractAttribute>())
            {
                throw new InvalidOperationException(
                    $"Service interface {type.Name} is not marked with ServiceContract attribute. Is this a proper WCF service interface?");
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
            var parameters = methodInfo
                .GetParameters()
                .ToArray();

            var parameterTypes = parameters
                .Select(m => m.ParameterType)
                .ToArray();

            // Task based async methods cannot have byref parameters
            if (parameterTypes.Any(m => m.IsByRef))
                return;

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
                methodBuilder.DefineParameter(i, ParameterAttributes.None, parameters[i-1].Name);

            Func<CustomAttributeBuilder> cloneOperationContractAttribute = () =>
            {
                var originalOperationContract = methodInfo.GetCustomAttribute<OperationContractAttribute>();

                Type attrType = typeof(OperationContractAttribute);
                var attributeCtor = attrType
                    .GetConstructor(Type.EmptyTypes);

                var actionProp = attrType.GetProperty(nameof(OperationContractAttribute.Action));
                var replyActionProp = attrType.GetProperty(nameof(OperationContractAttribute.ReplyAction));
                var nameProp = attrType.GetProperty(nameof(OperationContractAttribute.Name));
                var isOneWayProp = attrType.GetProperty(nameof(OperationContractAttribute.IsOneWay));
                var isInitiatingProp = attrType.GetProperty(nameof(OperationContractAttribute.IsInitiating));
                var isTerminatingProp = attrType.GetProperty(nameof(OperationContractAttribute.IsTerminating));

                string actionValue = GetOperationContractAction(methodInfo);
                string replyActionValue = GetOperationContractReplyAction(methodInfo);

                var propertyInfos = new List<PropertyInfo>
                {
                    actionProp,
                    isOneWayProp,
                    isInitiatingProp,
                    isTerminatingProp
                };

                var propertyValues = new List<object>
                {
                    actionValue,
                    originalOperationContract.IsOneWay,
                    originalOperationContract.IsInitiating,
                    originalOperationContract.IsTerminating
                };

                if (!originalOperationContract.IsOneWay)
                {
                    propertyInfos.Add(replyActionProp);
                    propertyValues.Add(replyActionValue);
                }

                if (!string.IsNullOrEmpty(originalOperationContract.Name))
                {
                    propertyInfos.Add(nameProp);
                    propertyValues.Add(originalOperationContract.Name);
                }

                var attributeBuilder = new CustomAttributeBuilder(
                    attributeCtor,
                    new object[0],
                    propertyInfos.ToArray(),
                    propertyValues.ToArray());

                return attributeBuilder;
            };

            methodBuilder.SetCustomAttribute(cloneOperationContractAttribute());

            var restrictedAttributeTypes = new[]
            {
                typeof(OperationContractAttribute),
                typeof(FaultContractAttribute),
                typeof(ServiceKnownTypeAttribute)
            };

            var remainingCustomAttributes = methodInfo
                .GetCustomAttributesData()
                .Where(m => !restrictedAttributeTypes.Contains(m.AttributeType));

            foreach (var builder in CloneCustomAttributes(remainingCustomAttributes))
                methodBuilder.SetCustomAttribute(builder);
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
            var parameters = methodInfo
                .GetParameters()
                .ToArray();

            var parameterTypes = parameters
                .Select(m => m.ParameterType)
                .ToArray();

            // TReturn Method(TParamType1 arg1, TParamType2 arg2, ...) {
            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                methodInfo.ReturnType,
                parameterTypes);

            for (int i = 1; i <= parameters.Length; i++)
                methodBuilder.DefineParameter(i, ParameterAttributes.None, parameters[i-1].Name);

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

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                Type parameterType = parameterTypes[i];
                if (parameterType.IsByRef)
                    continue;
                
                var handleRequestParameterMethod = typeof(RetryingWcfActionInvokerProvider<>)
                    .MakeGenericType(asyncInterfaceType)
                    .GetMethod("HandleRequestArgument", BindingFlags.Instance | BindingFlags.NonPublic)
                    .MakeGenericMethod(parameterType);

                ilGenerator.Emit(OpCodes.Ldarg, 0);
                ilGenerator.Emit(OpCodes.Ldarg, i + 1);
                ilGenerator.Emit(OpCodes.Ldstr, parameters[i].Name);
                ilGenerator.Emit(OpCodes.Call, handleRequestParameterMethod);
                ilGenerator.Emit(OpCodes.Starg_S, i + 1);
            }

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
                //.MakeGenericType(typeof(TServiceInterface))
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
                if (methodInfo.ReturnType.IsGenericType)
                {
                    var funcInvokeAsyncMethod = (typeof(IActionInvoker<TServiceInterface>))
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .First(m => m.Name == "InvokeAsync" && m.ReturnType != typeof(Task));

                    var taskReturnType = methodInfo.ReturnType.GenericTypeArguments[0];

                    return funcInvokeAsyncMethod.MakeGenericMethod(new[] { taskReturnType });                    
                }
                else
                {
                    var invokeAsyncMethod = typeof(IActionInvoker<TServiceInterface>)
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .First(m => m.Name == "InvokeAsync" && m.ReturnType == typeof(Task));

                    return invokeAsyncMethod;
                }
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

            string className = $"{methodInfo.Name}_{string.Join("_", parameterTypes.Select(m => m.Name))}";

            string typeName =
                $"WcfClientProxyGenerator.DynamicProxy.{typeof(TServiceInterface).Name}Support.{className}";

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
