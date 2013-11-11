WcfClientProxyGenerator
=========================
Utility to generate fault tolerant and retry capable dynamic proxies for WCF services based on the WCF service interface. 

With normal Service Reference or ChannelFactory instantiated clients, care must be taken to abort and recreate the client in the event that a communication fault occurs. The goal of this project is to provide an easy-to-use method of creating WCF clients that are self healing and tolerant of temporary network communication errors while still being as transparently useable as default WCF clients.

Installation
------------

    NuGet> Install-Package WcfClientProxyGenerator

Usage
-----
To create a proxy, use the `WcfClientProxy.Create<TServiceInterface>()` method. There are multiple overloads that can be used to setup and configure the proxy.

#### WcfClientProxy.Create\<TServiceInterface\>()
Calling create without passing any configuration in will configure the proxy using the `endpoint` section in your config where the `contract` attribute matches `TServiceInterface`. If more than one `endpoint` section exists, an `InvalidOperationException` is thrown. The alternate overloads must be used to select the appropriate endpoint configuration.

#### WcfClientProxy.Create\<TServiceInterface\>(string endpointConfigurationName)
This is a shortcut to using the overload that accepts an `Action<IRetryingProxyConfigurator>`. It's the same as calling `WcfClientProxy.Create<TServiceInterface>(c => c.SetEndpoint(endpointConfigurationName))`.

#### WcfClientProxy.Create\<TServiceInterface\>(Action\<IRetryingProxyConfigurator\> config)
Exposes the full configuration available. See the [Configuration](#configuration) section of the documentation.
	
Configuration
-------------
When calling the `WcfClientProxy.Create<TServiceInterface>()` method, a configuration Action is used to setup the proxy. The following configuration options are available at the proxy creation time:

If no configurator is given, then a `client` configuration section with the full name of the service interface type is looked for. If no `client` configuration section is present, an `InvalidOperationException` is thrown.

#### SetEndpoint(string endpointConfigurationName)
Configures the proxy to communicate with the endpoint as configured in the _app.config_ or _web.config_ `<system.serviceModel><client>` section. The `endpointConfigurationName` value needs to match the _name_ attribute value of the `<endpoint/>`.

For example, using:

    var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint("WSHttpBinding_ITestService"))

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

#### RetryOnException\<TException\>(Predicate\<TException\> where = null)
Configures the proxy to retry calls when it encounters arbitrary exceptions. The optional `Predicate<Exception>` can be used to refine properties of the Exception that it should retry on.

By default, if the following Exceptions are encountered while calling the service, the call will retry up to 5 times:

* ChannelTerminatedException
* EndpointNotFoundException
* ServerTooBusyException

#### RetryOnResponse\<TResponse\>(Predicate\<TResponse\> where)
Configures the proxy to retry calls based on conditions in the response from the service.

For example, if your response objects all inherit from a base `IResponseStatus` interface and you would like to retry calls when certain status codes are returned, the proxy can be configured as such:

    ITestService proxy = WcfClientProxy.Create<ITestService>(c =>
    {
        c.SetEndpoint("testServiceConfiguration");
        c.RetryOnResponse<IResponseStatus>(r => r.StatusCode == 503 || r.StatusCode == 504);
    });
    
The proxy will now retry calls made into the service when it detects a `503` or `504` status code.

#### SetDelayPolicy(Func\<IDelayPolicy\> policyFactory)
Configures how the proxy will handle pausing between failed calls to the WCF service. See the [Delay Policies](#delay-policies) section below.

Instances of `IDelayPolicy` are generated through the provided factory for each call made to the WCF service.

For example, to wait an exponentially growing amount of time starting at 500 milliseconds between failures:

	ITestService proxy = WcfClientProxy.Create<ITestService>(c =>
    {
    	c.SetDelayPolicy(() => new ExponentialBackoffDelayPolicy(TimeSpan.FromMilliseconds(500)));
    });

#### OnBeforeInvoke & OnAfterInvoke
Allows you to configure an event handlers that are called every time a method is called on the service.
Events will receive information which method was called and with what parameters in the `OnInvokeHandlerArguments` structure.

The OnBeforeInvoke event will fire every time a method is attempted to be called, and thus can be fired multiple times if you have a retry policy in place.
The OnAfterInvoke event will fire once after a successful call to a service method.

For example, to log all service calls:

````csharp
ITestService proxy = WcfClientProxy.Create<ITestService>(c =>
     c.OnBeforeInvoke += (sender, args) => {
        Console.WriteLine("{0}.{1} called with parameters: {2}",
            args.ServiceType.Name, args.InvokeInfo.MethodName,
            String.Join(", ", args.InvokeInfo.Parameters));
    };
    c.OnAfterInvoke += (sender, args) => {
        Console.WriteLine("{0}.{1} returned value: {2}",
            args.ServiceType.Name, args.InvokeInfo.MethodName,
            args.InvokeInfo.ReturnValue);
    };
});
int result = proxy.AddNumbers(3, 42);
````

Will print:

    ITestService.TestMethod called with parameters: 3, 42
    ITestService.TestMethod returned value: 45

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

    ITestService proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(binding, endpointAddress));

The proxy generated is now tolerant of faults and communication exceptions. In this example, if the first request results in a faulted channel, you would normally have to manually dispose of it. With the proxy instance, you can continue using it.

    ITestService proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint("testServiceConfiguration"));
    var response = proxy.ServiceMethod("request");
    var response2 = proxy.ServiceMethod("request2"); // even if the previous request resulted in a FaultException this call will still work

If there are known exceptions that you would like the proxy to retry calls on, it can be configured to retry when a custom exception is encountered:

    var proxy = WcfClientProxy.Create<ITestService>(c =>
    {
        c.SetEndpoint("testServiceConfiguration");
        c.RetryOnException<CustomException>();
        c.RetryOnException<PossibleCustomException>(e => e.Message == "retry only for this message");
    });


Delay Policies
--------------
Delay policies are classes that implement the `WcfClientProxyGenerator.Policy.IDelayPolicy` interface. There are a handful of pre-defined delay policies to use in this namespace.

If not specified, the `LinearBackoffDelayPolicy` will be used with a minimum delay of 500 milliseconds and a maximum of 10 seconds.


#### ConstantDelayPolicy
Waits a constant amount of time between call failures regardless of how many calls have failed.

#### LinearBackoffDelayPolicy
Waits a linearly increasing amount of time between call failures that grows based on how many previous calls have failed. This policy also accepts a maximum delay argument which insures the policy will never wait more than the defined maximum value.

#### ExponentialBackoffDelayPolicy
Same as the LinearBackoffDelayPolicy, but increases the amount of time between call failures exponentially.


License
-------
Apache 2.0
