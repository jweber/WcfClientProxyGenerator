using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator
{
    public class Generator<TServiceInterface>
        where TServiceInterface : class
    {
        public TServiceInterface Generate()
        {
            var assemblyName = new AssemblyName("temp");
            var appDomain = System.Threading.Thread.GetDomain();
            var assemblyBuilder = appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            var typeBuilder = moduleBuilder.DefineType(
                "Generated",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(Caller));

            typeBuilder.AddInterfaceImplementation(typeof(TServiceInterface));

            GenerateConstructor(typeBuilder);

            var serviceMethods = typeof(TServiceInterface)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(t => t.GetCustomAttribute<OperationContractAttribute>() != null);

            foreach (var serviceMethod in serviceMethods)
            {
                GenerateMethod(serviceMethod, typeBuilder);
            }

            Type generatedType = typeBuilder.CreateType();
            var inst = Activator.CreateInstance(generatedType, new object[] { "testConfig" }) as TServiceInterface;

            return inst;
        }

        private void GenerateConstructor(TypeBuilder typeBuilder)
        {
            var parameters = new[] { typeof(string) };
            var constructorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public, 
                CallingConventions.Standard, 
                parameters);

            var ilGenerator = constructorBuilder.GetILGenerator();

            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_1);

            var baseConstructor = typeof(Caller)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, parameters, null);

            ilGenerator.Emit(OpCodes.Call, baseConstructor);
            ilGenerator.Emit(OpCodes.Ret);
        }

        private void GenerateMethod(MethodInfo methodInfo, TypeBuilder typeBuilder)
        {
            var parameterTypes = methodInfo.GetParameters().Select(m => m.ParameterType).ToArray();

            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                methodInfo.ReturnType,
                parameterTypes);

            //methodBuilder.SetReturnType(methodInfo.ReturnType);

            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            //ilGenerator.Emit(OpCodes.Ldarg_1);

            var callerMethod = typeof(Caller).GetMethod("Get");
            

            ilGenerator.DeclareLocal(typeof(string));

            //ilGenerator.Emit(OpCodes.Ldloc_0);

            for (int i = 0; i < parameterTypes.Length; i++)
                ilGenerator.Emit(OpCodes.Ldarg, ((short) i + 1));

            ilGenerator.Emit(OpCodes.Call, callerMethod);

            ilGenerator.Emit(OpCodes.Stloc_0);
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder, methodInfo);
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

    public class TestImpl : Caller, ITest
    {
        public TestImpl(string c)
            : base(c)
        {}

        public string Get(string arg)
        {
            return base.Get(arg);
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
