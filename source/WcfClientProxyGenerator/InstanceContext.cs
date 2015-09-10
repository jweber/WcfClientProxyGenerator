using System.ServiceModel;

namespace WcfClientProxyGenerator
{

    /// <summary>
    /// Generic instance context for callback services.
    /// </summary>
    /// <typeparam name="TCallback">the type of the callback context.</typeparam>
    public class InstanceContext<TCallback>
    {
        private readonly InstanceContext _context;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callbackInstance"></param>
        public InstanceContext(TCallback callbackInstance)
        {
            _context = new InstanceContext(callbackInstance);
        }

        /// <summary>
        /// Get the instance context.
        /// </summary>
        public InstanceContext Context => _context;

        /// <summary>
        /// Gets the service instance.
        /// </summary>
        public TCallback ServiceInstance => (TCallback)_context.GetServiceInstance();

        /// <summary>
        /// Releases the underlying service instance.
        /// </summary>
        public void ReleaseServiceInstance()
        {
            _context.ReleaseServiceInstance();
        }
    }
}