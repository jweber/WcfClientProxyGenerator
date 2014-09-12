using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

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

        public Task<TResponse> CallAsync<TResponse>(Expression<Func<TServiceInterface, TResponse>> method)
        {
            var methodCall = method.Body as MethodCallExpression;

            var asyncMethod = this.provider.GetType().GetMethod(methodCall.Method.Name + "Async");
            object[] args = methodCall.Arguments.Select(m => m).ToArray();

            var parameter = Expression.Parameter(this.provider.GetType(), "svc");
            var asyncCall = Expression
                .Call(parameter, asyncMethod, methodCall.Arguments);

            var l = Expression.Lambda(asyncCall, parameter);
            var cl = l.Compile();
            var r = cl.DynamicInvoke(this.provider);

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
