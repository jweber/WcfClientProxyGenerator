using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator
{
    public class Generator<TServiceInterface>
        where TServiceInterface : class
    {
        public TServiceInterface Generate(Binding binding, EndpointAddress endpointAddress)
        {
            var assemblyName = new AssemblyName("WcfClientProxyGenerator.DynamicProxy");
            var appDomain = System.Threading.Thread.GetDomain();
            var assemblyBuilder = appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, "WcfClientProxyGenerator.DynamicProxy.dll");

            var typeBuilder = moduleBuilder.DefineType(
                "-proxy-" + typeof(TServiceInterface).Name,
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(FaultCaller<TServiceInterface>));

            typeBuilder.AddInterfaceImplementation(typeof(TServiceInterface));

            GenerateConstructor(typeBuilder);

            var serviceMethods = typeof(TServiceInterface)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(t => t.GetCustomAttribute<OperationContractAttribute>() != null);

            foreach (var serviceMethod in serviceMethods)
            {
                GenerateMethod(serviceMethod, typeBuilder, moduleBuilder);
            }

            Type generatedType = typeBuilder.CreateType();

#if DEBUG
            assemblyBuilder.Save("WcfClientProxyGenerator.DynamicProxy.dll");
#endif
            
            var inst = Activator.CreateInstance(generatedType, new object[] { binding, endpointAddress }) as TServiceInterface;

            return inst;
        }

        private void GenerateConstructor(TypeBuilder typeBuilder)
        {
            var parameters = new[] { typeof(Binding), typeof(EndpointAddress) };
            var constructorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public, 
                CallingConventions.Standard, 
                parameters);

            var ilGenerator = constructorBuilder.GetILGenerator();

            ilGenerator.Emit(OpCodes.Ldarg_0); // this
            ilGenerator.Emit(OpCodes.Ldarg_1); // binding parameter
            ilGenerator.Emit(OpCodes.Ldarg_2); // endpoint address parameter

            var baseConstructor = typeof(FaultCaller<TServiceInterface>)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, parameters, null);

            ilGenerator.Emit(OpCodes.Call, baseConstructor);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private void GenerateMethod(MethodInfo methodInfo, TypeBuilder typeBuilder, ModuleBuilder moduleBuilder)
        {
            var parameterTypes = methodInfo.GetParameters().Select(m => m.ParameterType).ToArray();

            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                methodInfo.ReturnType,
                parameterTypes);

            var lambdaTypeBuilder = moduleBuilder.DefineType("-lambda-" + methodInfo.Name);
            // define lambda arguments
            //var lambdaField = lambdaTypeBuilder.DefineField("arg", typeof(string), FieldAttributes.Public);
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
            lambdaIlGenerator.Emit(OpCodes.Ldarg_0);
            
            lambdaFields.ForEach(lf => lambdaIlGenerator.Emit(OpCodes.Ldfld, lf));

            lambdaIlGenerator.Emit(OpCodes.Callvirt, methodInfo);
            lambdaIlGenerator.Emit(OpCodes.Stloc_0);
            lambdaIlGenerator.Emit(OpCodes.Ldloc_0);
            lambdaIlGenerator.Emit(OpCodes.Ret);

            Type lambdaType = lambdaTypeBuilder.CreateType();

            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.DeclareLocal(typeof(RetryingWcfActionInvoker<TServiceInterface>));
            ilGenerator.DeclareLocal(typeof(Func<,>).MakeGenericType(typeof(TServiceInterface), methodInfo.ReturnType));
            ilGenerator.DeclareLocal(lambdaType);
            ilGenerator.DeclareLocal(methodInfo.ReturnType);

            var lambdaCtor = lambdaType.GetConstructor(Type.EmptyTypes);

            ilGenerator.Emit(OpCodes.Newobj, lambdaCtor);
            ilGenerator.Emit(OpCodes.Stloc_2);
            ilGenerator.Emit(OpCodes.Ldloc_2);
            ilGenerator.Emit(OpCodes.Ldarg_1);

            lambdaFields.ForEach(lf => ilGenerator.Emit(OpCodes.Stfld, lambdaType.GetField(lf.Name)));

            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Ldarg_0);

            var channelProperty = typeof(FaultCaller<TServiceInterface>)
                .GetMethod(
                    "get_ActionInvoker", 
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty);

            ilGenerator.Emit(OpCodes.Call, channelProperty);
            ilGenerator.Emit(OpCodes.Stloc_0);
            ilGenerator.Emit(OpCodes.Ldloc_2);

//            for (int i = 0; i < parameterTypes.Length; i++)
//                ilGenerator.Emit(OpCodes.Ldarg, ((short) i + 1));

            var lambdaGetMethod = lambdaType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
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

            //typeBuilder.DefineMethodOverride(methodBuilder, methodInfo);
        }

        private bool MethodHasReturnValue(MethodInfo methodInfo)
        {
            return methodInfo.ReturnType != typeof(void);
        }
    }

    public interface ITest
    {
        string Get(string arg);
    }

    internal class TestImpl : FaultCaller<ITest>, ITest
    {
        public TestImpl(Binding binding, EndpointAddress endpointAddress)
            : base(binding, endpointAddress)
        {}

        public string Get(string arg)
        {
            var invokerInstance = base.ActionInvoker;
            Func<ITest, string> lambda = m => m.Get(arg);
            
            return invokerInstance.Invoke(lambda);
        }
    }

    internal class FaultCaller<TServiceInterface>
        where TServiceInterface : class
    {
        private Binding _binding;
        private EndpointAddress _endpointAddress;

        protected FaultCaller(Binding binding, EndpointAddress endpointAddress)
        {
            _binding = binding;
            _endpointAddress = endpointAddress;
        }

        protected TServiceInterface Proxy
        {
            get
            {
                var cf = new ChannelFactory<TServiceInterface>(_binding, _endpointAddress);
                return cf.CreateChannel();
            }
        }

        protected RetryingWcfActionInvoker<TServiceInterface> ActionInvoker
        {
            get
            {
                var ai = new RetryingWcfActionInvoker<TServiceInterface>(() => new ChannelFactory<TServiceInterface>(_binding, _endpointAddress).CreateChannel());
                return ai;               
            }
        }
    }

    public class Caller
    {
        private string _endpointConfigurationName;

        protected Caller(string endpointConfigurationName)
        {
            _endpointConfigurationName = endpointConfigurationName;
        }

        public string Get(string arg)
        {
            return _endpointConfigurationName + ": " + arg;
        }
    }
}
