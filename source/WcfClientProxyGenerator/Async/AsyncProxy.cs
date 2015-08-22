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
    /// Wrapper around <c>TServiceInterface</c> providing an async friendly
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
        /// Make a <see cref="Task"/> based async call to a WCF method on <c>TServiceInterface</c>
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="method"></param>
        /// <returns></returns>
        Task<TResponse> CallAsync<TResponse>(Expression<Func<TServiceInterface, TResponse>> method);

        /// <summary>
        /// Make a <see cref="Task"/> based async call to a WCF method on <c>TServiceInterface</c>
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        Task CallAsync(Expression<Action<TServiceInterface>> method);
    }

    class AsyncProxy<TServiceInterface> : IAsyncProxy<TServiceInterface>
        where TServiceInterface : class
    {
        internal static readonly ConcurrentDictionary<int, Lazy<Delegate>> DelegateCache
            = new ConcurrentDictionary<int, Lazy<Delegate>>();

        private readonly TServiceInterface provider;

        public AsyncProxy(TServiceInterface provider)
        {
            this.provider = provider;
        }

        public TServiceInterface Client { get { return this.provider; } }

        public Task<TResponse> CallAsync<TResponse>(Expression<Func<TServiceInterface, TResponse>> method)
        {
            return this.InvokeCallAsyncDelegate<Task<TResponse>>(method.Body);
        }

        public Task CallAsync(Expression<Action<TServiceInterface>> method)
        {
            return this.InvokeCallAsyncDelegate<Task>(method.Body);
        }

        private TResponse InvokeCallAsyncDelegate<TResponse>(Expression expression)
            where TResponse : Task
        {
            var methodCall = expression as MethodCallExpression;
            if (methodCall == null)
                throw new NotSupportedException("Calls made to .CallAsync() must be of type 'MethodCallExpression'");

            var methodParameters = methodCall.Method.GetParameters().ToArray();

            if (methodParameters.Any(m => m.ParameterType.IsByRef))
            {
                throw new NotSupportedException(
                    string.Format("OperationContract method '{0}' has parameters '{1}' marked as out or ref. These are not currently supported in async calls.",
                        methodCall.Method.Name, 
                        string.Join(", ", methodParameters.Where(m => m.ParameterType.IsByRef).Select(m => m.Name))));
            }

            var cachedDelegate = this.GetCallAsyncDelegate(methodCall, methodParameters);
            var argumentValues = this.GetMethodCallArgumentValues(methodCall);

            var invokeArgs = new object[] { this.provider }.Concat(argumentValues);
            return cachedDelegate.DynamicInvoke(invokeArgs.ToArray()) as TResponse;
        }

        private Delegate GetCallAsyncDelegate(MethodCallExpression methodCall, ParameterInfo[] methodParameters)
        {
            int cacheKey = GetMethodCallExpressionHashCode(methodCall, methodParameters.Select(m => m.ParameterType));
            return DelegateCache.GetOrAddSafe(cacheKey, _ =>
            {
                var methodParameterTypes = methodParameters.Select(m => m.ParameterType).ToArray();
                var methodInfo = this.provider.GetType().GetMethod(methodCall.Method.Name + "Async", methodParameterTypes)
                                 ?? this.provider.GetType().GetMethod(methodCall.Method.Name, methodParameterTypes);

                if (methodInfo == null)
                    throw new NotSupportedException(
                        string.Format("CallAsync could not locate the appropriate method based on '{0}' to call asynchronously", methodCall.Method.Name));

                if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
                    throw new NotSupportedException(
                        string.Format("Method '{0}' has return type of '{1}' which is not based on 'Task' and cannot be used for asynchronous calls",
                            methodInfo.Name,
                            methodInfo.ReturnType.ToString()));

                var proxyParam = Expression.Parameter(this.provider.GetType(), "proxy");

                var delegateParameters = new ParameterExpression[methodCall.Arguments.Count];
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    delegateParameters[i] = Expression.Parameter(
                        methodParameters[i].ParameterType, 
                        methodParameters[i].Name);
                }

                var invokeExpr = Expression.Call(proxyParam, methodInfo, delegateParameters);

                var lambdaParamaters = new[] { proxyParam }.Concat(delegateParameters);
                var lambdaExpression = Expression.Lambda(invokeExpr, lambdaParamaters);
                Delegate @delegate = lambdaExpression.Compile();

                return @delegate;
            });
        }

        private object[] GetMethodCallArgumentValues(MethodCallExpression methodCall)
        {
            var arguments = from arg in methodCall.Arguments
                            let argAsObj = Expression.Convert(arg, typeof(object))
                            select Expression.Lambda<Func<object>>(argAsObj, null)
                                .Compile()();

            return arguments.ToArray();
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
