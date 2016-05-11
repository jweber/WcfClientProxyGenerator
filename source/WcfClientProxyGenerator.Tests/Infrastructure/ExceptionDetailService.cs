using System.ServiceModel;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    [ServiceContract]
    public interface IExceptionDetailService
    {
        [OperationContract]
        string Method(string input);
    }

    /// <summary>
    /// Enabling IncludeExceptionDetailInFaults. Can't do this through a substitute
    /// </summary>
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class ExceptionDetailService : IExceptionDetailService
    {
        private readonly IExceptionDetailService _sub;

        public ExceptionDetailService(IExceptionDetailService sub)
        {
            _sub = sub;
        }

        public string Method(string input) => _sub.Method(input);
    }
}