WcfClientProxyGenerator
=========================
Utility to generate fault tolerant and retry capable dynamic proxies for WCF services based on the WCF service interface. 

With normal Service Reference or ChannelFactory instantiated clients, care must be taken to abort and recreate the client in the event that a communication fault occurs. The goal of this project is to provide an easy-to-use method of creating WCF clients that are self healing and tolerant of temporary network communication errors while still being as transparently useable as default WCF clients.

Installation
------------

    Install-Package WcfClientProxyGenerator

Configuration
-------------
When calling the `WcfClientProxyGenerator.Create<TServiceInterface>()` method, a configuration Action is used to setup the proxy. The following configuration options are available at the proxy creation time:

#### SetEndpoint(string endpointConfigurationName)
Configures the proxy to communicate with the endpoint as configured in the _app.config_ or _web.config_ `<system.serviceModel><client>` section. The `endpointConfigurationName` value needs to match the _name_ attribute value of the `<endpoint/>`.

For example, using:

    var proxy = WcfClientProxyGenerator.Create<ITestService>(c => c.SetEndpoint("WSHttpBinding_ITestService"))

will configure the proxy based on the `<endpoint/>` as setup in the _app.config_:

    <?xml version="1.0" encoding="utf-8" ?>
    <configuration>
        <system.serviceModel>
            <client>
                <endpoint name="WSHttpBinding_ITestService"
                          address="http://localhost:23456/TestService" 
                          binding="wsHttpBinding" 
                          contract="Api.TestService.ITestService"/>
            </client>
        </system.serviceModel>
    </configuration>

#### SetEndpoint(Binding binding, EndpointAddress endpointAddress)
Configures the proxy to communicate with the endpoint using the given `binding` at the `endpointAddress`

#### MaximumRetries(int retryCount)
Sets the maximum amount of times the the proxy will attempt to call the service in the event it encounters a known retry-friendly exception.

#### TimeBetweenRetries(TimeSpan timeSpan)
Sets the minimum amount of time to pause between retrying calls to the service. This amount of time is multiplied by the current iteration of the retryCount to perform a linear back-off.

#### RetryOnException<TException>(Predicate<Exception> where = null)
Configures the proxy to retry calls when it encounters arbitrary exceptions. The optional `Predicate<Exception>` can be used to refine properties of the Exception that it should retry on.

By default, if the following Exceptions are encountered while calling the service, the call will retry up to 5 times:

* ChannelTerminatedException
* EndpointNotFoundException
* ServerTooBusyException

Examples
--------
The following interface defines the contract for the service:

    [ServiceContract]
    public interface ITestService
    {
        [OperationContract]
        string ServiceMethod(string request);
    }

The proxy can then be created based on this interface by using the `Create` method of the proxy generator:

    ITestService proxy = WcfClientProxyGenerator.Create<ITestService>(c => c.SetEndpoint(binding, endpointAddress));

The proxy generated is now tolerant of faults and communication exceptions. In this example, if the first request results in a faulted channel, you would normally have to manually dispose of it. With the proxy instance, you can continue using it.

    ITestService proxy = WcfClientProxyGenerator.Create<ITestService>(c => c.SetEndpoint("testServiceConfiguration"));
    var response = proxy.ServiceMethod("request");
    var response2 = proxy.ServiceMethod("request2"); // even if the previous request resulted in a FaultException this call will still work

If there are known exceptions that you would like the proxy to retry calls on, it can be configured to retry when a custom exception is encountered:

    var proxy = WcfClientProxyGenerator.Create<ITestService>(c =>
    {
        c.SetEndpoint("testServiceConfiguration");
        c.RetryOnException<CustomException>();
        c.RetryOnException<PossibleCustomException>(e => e.Message == "retry only for this message");
    });

License
-------
Apache 2.0
