using System.ServiceModel;
using WcfClientProxyGenerator.Tests.Services;

namespace WcfClientProxyGenerator.Tests.WcfServiceHost.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Reentrant)]
    public class DuplexService : IDuplexService
    {
        public string Test(string input)
        {
            var callBackResponse = Callback.TestCallback(input);
            return $"Method Echo: {callBackResponse}";
        }

        public void OneWay(string input)
        {
            Callback.OneWayCallback(input);
        }

        IDuplexServiceCallback Callback => OperationContext.Current.GetCallbackChannel<IDuplexServiceCallback>();
    }
}