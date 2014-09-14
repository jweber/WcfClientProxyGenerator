using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

        private static readonly ConcurrentDictionary<int, Lazy<Delegate>> CallAsyncFuncCache 
            = new ConcurrentDictionary<int, Lazy<Delegate>>(); 


        public Task<TResponse> CallAsync<TResponse>(Expression<Func<TServiceInterface, TResponse>> method)
        {
            var methodCall = method.Body as MethodCallExpression;
            
            var args = (from arg in methodCall.Arguments
                        let argAsObj = Expression.Convert(arg, typeof(object))
                        select Expression.Lambda<Func<object>>(argAsObj, null)
                            .Compile()()).ToArray();

            int offset = methodCall.Method.GetHashCode();
            int key = offset;
//            foreach (object o in args)
//                key = key ^ (o == null ? offset : o.GetType().GetHashCode() << offset++);

            var cl = CallAsyncFuncCache.GetOrAddSafe(key, _ =>
            {
                var asyncMethod = this.provider.GetType().GetMethod(methodCall.Method.Name + "Async");

                var proxyParam = Expression.Parameter(this.provider.GetType(), "proxy");

                var methodArgs = new ParameterExpression[methodCall.Arguments.Count];
                for (int i = 0; i < args.Length; i++)
                {
                    methodArgs[i] = Expression.Parameter(args[i].GetType(), "arg" + (i+1));
                }

                var asyncCall = Expression
                    .Call(proxyParam, asyncMethod, methodArgs);

                var lambdaArgs = new List<ParameterExpression>(methodArgs.Length + 1);
                lambdaArgs.Add(proxyParam);
                lambdaArgs.AddRange(methodArgs);

                var l = Expression.Lambda(asyncCall, lambdaArgs);
                Delegate del = l.Compile();

                return del;
            });



            var invokeArgs = new List<object>();
            invokeArgs.Add(this.provider);
            invokeArgs.AddRange(args);

            var r = cl.DynamicInvoke(invokeArgs.ToArray());

            //var asyncMethod = this.provider.GetType().GetMethod("TestMethodAsync");
            return r as Task<TResponse>;
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
    }
}
