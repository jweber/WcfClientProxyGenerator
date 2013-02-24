using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace WcfClientProxyGenerator
{
    internal static class InternalProxyGenerator<TServiceInterface>
        where TServiceInterface : class
    {
        public static Type GenerateType()
        {
            var assemblyName = new AssemblyName("WcfClientProxyGenerator.DynamicProxy");
            var appDomain = System.Threading.Thread.GetDomain();
            var assemblyBuilder = appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, "WcfClientProxyGenerator.DynamicProxy.dll");
            
            var typeBuilder = moduleBuilder.DefineType(
                "-proxy-" + typeof(TServiceInterface).Name,
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(ActionInvokerProvider<TServiceInterface>));
            
            typeBuilder.AddInterfaceImplementation(typeof(TServiceInterface));

            SetDebuggerDisplay(typeBuilder, typeof(TServiceInterface).Name + " (wcf proxy)");

            GenerateTypeConstructor(typeBuilder, typeof(string));
            GenerateTypeConstructor(typeBuilder, typeof(Binding), typeof(EndpointAddress));

            var serviceMethods = typeof(TServiceInterface)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(t => t.GetCustomAttribute<OperationContractAttribute>() != null);

            foreach (var serviceMethod in serviceMethods)
            {
                GenerateServiceProxyMethod(serviceMethod, moduleBuilder, typeBuilder);
            }

            Type generatedType = typeBuilder.CreateType();

#if DEBUG
            assemblyBuilder.Save("WcfClientProxyGenerator.DynamicProxy.dll");
#endif

            return generatedType;
        }

        private static void SetDebuggerDisplay(TypeBuilder typeBuilder, string display)
        {
            var ca = typeof(DebuggerDisplayAttribute).GetConstructor(new[] { typeof(string) });
            var cab = new CustomAttributeBuilder(ca, new object[] { display });
            typeBuilder.SetCustomAttribute(cab);
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

            var baseConstructor = typeof(ActionInvokerProvider<TServiceInterface>)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, argumentParameterTypes, null);

            ilGenerator.Emit(OpCodes.Call, baseConstructor);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void GenerateServiceProxyMethod(MethodInfo methodInfo, ModuleBuilder moduleBuilder, TypeBuilder typeBuilder)
        {
            var parameterTypes = methodInfo.GetParameters().Select(m => m.ParameterType).ToArray();

            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                methodInfo.ReturnType,
                parameterTypes);

            Type actionInvokerLambdaType;
            var actionInvokerLambdaFields = GenerateActionInvokerLambdaType(methodInfo, moduleBuilder, parameterTypes, out actionInvokerLambdaType);

            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.DeclareLocal(typeof(RetryingWcfActionInvoker<TServiceInterface>));
            ilGenerator.DeclareLocal(typeof(Func<,>).MakeGenericType(typeof(TServiceInterface), methodInfo.ReturnType));
            ilGenerator.DeclareLocal(actionInvokerLambdaType);
            ilGenerator.DeclareLocal(methodInfo.ReturnType);

            var lambdaCtor = actionInvokerLambdaType.GetConstructor(Type.EmptyTypes);

            ilGenerator.Emit(OpCodes.Newobj, lambdaCtor);
            ilGenerator.Emit(OpCodes.Stloc_2);
            ilGenerator.Emit(OpCodes.Ldloc_2);
            
            for (int i = 0; i < actionInvokerLambdaFields.Count; i++)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i + 1);
                ilGenerator.Emit(OpCodes.Stfld, actionInvokerLambdaType.GetField(actionInvokerLambdaFields[i].Name));

                if (i < actionInvokerLambdaFields.Count)
                    ilGenerator.Emit(OpCodes.Ldloc_2);
            }

            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Ldarg_0);

            var channelProperty = typeof(ActionInvokerProvider<TServiceInterface>)
                .GetMethod(
                    "get_ActionInvoker", 
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty);

            ilGenerator.Emit(OpCodes.Call, channelProperty);
            ilGenerator.Emit(OpCodes.Stloc_0);
            ilGenerator.Emit(OpCodes.Ldloc_2);

            var lambdaGetMethod = actionInvokerLambdaType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
            ilGenerator.Emit(OpCodes.Ldftn, lambdaGetMethod);
            
            // new func<TService, TReturn>
            var funcCons = typeof(Func<,>)
                .MakeGenericType(typeof(TServiceInterface), methodInfo.ReturnType)
                .GetConstructor(new Type[] { typeof(object), typeof(IntPtr) });

            ilGenerator.Emit(OpCodes.Newobj, funcCons);
            ilGenerator.Emit(OpCodes.Stloc_1);
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Ldloc_1);

            var invokeMethod = typeof(RetryingWcfActionInvoker<TServiceInterface>)
                .GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public)
                .MakeGenericMethod(new[] { methodInfo.ReturnType });

            ilGenerator.Emit(OpCodes.Callvirt, invokeMethod);

            ilGenerator.Emit(OpCodes.Stloc_3);
            ilGenerator.Emit(OpCodes.Ldloc_3);
            ilGenerator.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Builds the type used by the call to the <see cref="IActionInvoker{TServiceInterface}.Invoke{TResponse}"/>
        /// method.
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <param name="moduleBuilder"></param>
        /// <param name="parameterTypes"></param>
        /// <param name="lambdaType"></param>
        /// <returns></returns>
        private static IList<FieldBuilder> GenerateActionInvokerLambdaType(MethodInfo methodInfo, ModuleBuilder moduleBuilder, Type[] parameterTypes, out Type lambdaType)
        {
            var lambdaTypeBuilder = moduleBuilder.DefineType("-lambda-" + methodInfo.Name);

            var lambdaFields = new List<FieldBuilder>(parameterTypes.Length);
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                Type parameterType = parameterTypes[i];
                lambdaFields.Add(
                    lambdaTypeBuilder.DefineField("arg" + i, parameterType, FieldAttributes.Public));
            }

            var lambdaMethodBuilder = lambdaTypeBuilder.DefineMethod(
                "Get",
                MethodAttributes.Public,
                methodInfo.ReturnType,
                new[] { typeof(TServiceInterface) });

            var lambdaIlGenerator = lambdaMethodBuilder.GetILGenerator();
            lambdaIlGenerator.DeclareLocal(methodInfo.ReturnType);
            lambdaIlGenerator.Emit(OpCodes.Ldarg_1);

            lambdaFields.ForEach(lf =>
            {
                lambdaIlGenerator.Emit(OpCodes.Ldarg_0);
                lambdaIlGenerator.Emit(OpCodes.Ldfld, lf);
            });

            lambdaIlGenerator.Emit(OpCodes.Callvirt, methodInfo);
            lambdaIlGenerator.Emit(OpCodes.Stloc_0);
            lambdaIlGenerator.Emit(OpCodes.Ldloc_0);
            lambdaIlGenerator.Emit(OpCodes.Ret);

            lambdaType = lambdaTypeBuilder.CreateType();
            return lambdaFields;
        }
    }
}
