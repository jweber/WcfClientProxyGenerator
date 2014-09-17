using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator.Async
{
    /// <summary>
    /// Wrapper around <typeparamref name="TServiceInterface"/> providing an async friendly
    /// interface.
    /// </summary>
    /// <typeparam name="TServiceInterface"></typeparam>
    public interface IAsyncProxy<TServiceInterface>
        where TServiceInterface : class
    {
        /// <summary>
        /// Access to the underlying client proxy
        /// </summary>
        TServiceInterface Client { get; }

        /// <summary>
        /// Make a <see cref="Task"/> based async call to a WCF method on <see cref="TServiceInterface"/>
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="method"></param>
        /// <returns></returns>
        Task<TResponse> CallAsync<TResponse>(Expression<Func<TServiceInterface, TResponse>> method);
        
        /// <summary>
        /// Make a <see cref="Task"/> based async call to a WCF method on <see cref="TServiceInterface"/>
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        Task CallAsync(Expression<Action<TServiceInterface>> method);
    }

    class AsyncProxy<TServiceInterface> : IAsyncProxy<TServiceInterface>
        where TServiceInterface : class
    {
        private readonly TServiceInterface provider;

        public AsyncProxy(TServiceInterface provider)
        {
            this.provider = provider;
        }

        public TServiceInterface Client { get { return this.provider; } }

        // async call delegate / async call not supported reason
        private static readonly ConcurrentDictionary<int, Lazy<Delegate>> CallAsyncFuncCache 
            = new ConcurrentDictionary<int, Lazy<Delegate>>();

        public Task<TResponse> CallAsync<TResponse>(Expression<Func<TServiceInterface, TResponse>> method)
        {
            var methodCall = method.Body as MethodCallExpression;
            if (methodCall == null)
                throw new NotSupportedException("Calls made to .CallAsync() must be of type 'MethodCallExpression'");

            var methodParameters = methodCall.Method.GetParameters().ToArray();

            if (methodParameters.Any(m => m.ParameterType.IsByRef))
            {
                throw new NotSupportedException(
                    string.Format("OperationContract method '{0}' has parameters '{1}' marked as out or ref. These are not currently supported in async calls.",
                        methodCall.Method.Name, 
                        methodParameters.Where(m => m.ParameterType.IsByRef).Select(m => m.Name)));
            }

            int cacheKey = GetMethodCallExpressionHashCode(methodCall, methodParameters.Select(m => m.ParameterType));
            var cachedDelegate = CallAsyncFuncCache.GetOrAddSafe(cacheKey, _ =>
            {
                var methodParameterTypes = methodParameters.Select(m => m.ParameterType).ToArray();
                var methodInfo = this.provider.GetType().GetMethod(methodCall.Method.Name + "Async", methodParameterTypes)
                                 ?? this.provider.GetType().GetMethod(methodCall.Method.Name, methodParameterTypes);

                var proxyParam = Expression.Parameter(this.provider.GetType(), "proxy");

                var delegateParameters = new ParameterExpression[methodCall.Arguments.Count];
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    delegateParameters[i] = Expression.Parameter(
                        methodParameters[i].ParameterType, 
                        methodParameters[i].Name);
                }

                var invokeExpr = Expression.Call(proxyParam, methodInfo, delegateParameters);

                var lambdaArgs = new List<ParameterExpression>(delegateParameters.Length + 1);
                lambdaArgs.Add(proxyParam);
                lambdaArgs.AddRange(delegateParameters);

                var lambdaExpression = Expression.Lambda(invokeExpr, lambdaArgs);
                Delegate @delegate = lambdaExpression.Compile();

                return @delegate;
            });

            var argumentValues = (from arg in methodCall.Arguments
                                  let argAsObj = Expression.Convert(arg, typeof (object))
                                  select Expression.Lambda<Func<object>>(argAsObj, null)
                                      .Compile()()).ToArray();

            var invokeArgs = new object[] { this.provider }.Concat(argumentValues);
            return cachedDelegate.DynamicInvoke(invokeArgs.ToArray()) as Task<TResponse>;
        }

        public Task CallAsync(Expression<Action<TServiceInterface>> method)
        {
            var methodCall = method.Body as MethodCallExpression;

            var asyncMethod = this.provider.GetType().GetMethod(methodCall.Method.Name + "Async");

            var parameter = Expression.Parameter(this.provider.GetType(), "svc");
            var asyncCall = Expression
                .Call(parameter, asyncMethod, methodCall.Arguments);

            var l = Expression.Lambda(asyncCall, parameter);
            var cl = l.Compile();
            var r = cl.DynamicInvoke(this.provider);

            //var asyncMethod = this.provider.GetType().GetMethod("TestMethodAsync");
            return r as Task;
        }

        private static int GetMethodCallExpressionHashCode(MethodCallExpression methodCall, IEnumerable<Type> parameterTypes)
        {
            int offset = methodCall.Method.GetHashCode();
            int key = offset;
            foreach (var parameterType in parameterTypes)
                key = key ^ (parameterType == null ? offset : parameterType.GetHashCode() << offset++);

            return key;
        }
    }
}
