using System;
using System.Threading;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator.Async
{
    /// <summary>
    /// Wrapper around <typeparamref name="TServiceInterface"/> providing an async friendly
    /// interface.
    /// </summary>
    /// <typeparam name="TServiceInterface"></typeparam>
    public interface IAsyncProxy<out TServiceInterface>
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
        Task<TResponse> CallAsync<TResponse>(Func<TServiceInterface, TResponse> method);

        /// <summary>
        /// Make a <see cref="Task"/> based async call to a WCF method on <see cref="TServiceInterface"/>
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="method"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TResponse> CallAsync<TResponse>(Func<TServiceInterface, TResponse> method, CancellationToken cancellationToken);
        
        /// <summary>
        /// Make a <see cref="Task"/> based async call to a WCF method on <see cref="TServiceInterface"/>
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        Task CallAsync(Action<TServiceInterface> method);

        /// <summary>
        /// Make a <see cref="Task"/> based async call to a WCF method on <see cref="TServiceInterface"/>
        /// </summary>
        /// <param name="method"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CallAsync(Action<TServiceInterface> method, CancellationToken cancellationToken);
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

        public Task<TResponse> CallAsync<TResponse>(Func<TServiceInterface, TResponse> method)
        {
            return this.CallAsync(method, CancellationToken.None);
        }

        public Task<TResponse> CallAsync<TResponse>(Func<TServiceInterface, TResponse> method, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                state => method((TServiceInterface) state), 
                this.provider,
                cancellationToken, 
                TaskCreationOptions.DenyChildAttach, 
                TaskScheduler.Default);
        }

        public Task CallAsync(Action<TServiceInterface> method)
        {
            return this.CallAsync(method, CancellationToken.None);
        }

        public Task CallAsync(Action<TServiceInterface> method, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                state => method((TServiceInterface) state),
                this.provider,
                cancellationToken,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }
    }
}
