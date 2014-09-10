using System;
using System.Threading;
using System.Threading.Tasks;

namespace WcfClientProxyGenerator.Async
{
    public interface IAsyncProxy<out TServiceInterface>
        where TServiceInterface : class
    {
        Task<TResponse> CallAsync<TResponse>(Func<TServiceInterface, TResponse> method);
        Task<TResponse> CallAsync<TResponse>(Func<TServiceInterface, TResponse> method, CancellationToken cancellationToken);
        Task CallAsync(Action<TServiceInterface> method);
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
