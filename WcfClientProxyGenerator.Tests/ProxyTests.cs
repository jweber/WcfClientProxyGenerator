using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using WcfClientProxyGenerator.Policy;
using WcfClientProxyGenerator.Tests.Infrastructure;
using WcfClientProxyGenerator.Tests.Services;
using Xunit;

namespace WcfClientProxyGenerator.Tests
{
    public class ProxyTests : TestBase
    {
        public ProxyTests()
        {
            DynamicProxyAssembly.Initialize();
        }

#if NETFULL
        [Fact] 
        [Description("Asserts that when no conguration is given in the Create proxy call, the endpoint config that matches the contract will be used")]
        public void CreatingProxy_WithNoConfigurator_AndSingleEndpointConfig_GetsDefaultClientConfiguration()
        {
            WcfClientProxy.Create<ITestServiceSingleEndpointConfig>();
        }

        [Fact] 
        [Description("Asserts that when no conguration is given in the Create proxy call, and multiple endpoint configs for the contract exist, an exception is thrown")]
        public void CreatingProxy_WithNoConfigurator_AndMultipleEndpointConfigs_ThrowsException()
        {
            Should.Throw<InvalidOperationException>(() => WcfClientProxy.Create<ITestService>());
        }

        [Fact]
        public void CreatingProxy_WithNoConfigurator_AndNoDefaultConfiguration_ThrowsException()
        {
            Should.Throw<InvalidOperationException>(() => WcfClientProxy.Create<IChildService>());
        }

        [Fact]
        public void CreatingProxy_WithEndpointConfigurationName_ThatExists_CreatesProxy()
        {
            Should.NotThrow(() =>WcfClientProxy.Create<ITestService>("ITestService2"));
        }

        [Fact]
        public void CreatingProxy_WithEndpointConfigurationName_ThatDoesNotExist_ThrowsException()
        {
            Should.Throw<InvalidOperationException>(() => WcfClientProxy.Create<ITestService>("DoesNotExist"));
        }
    
#endif

        [Fact]
        public void CreatingProxy_WithServiceEndpoint_CreatesProxy()
        {
            ContractDescription contractDescription = ContractDescription.GetContract(typeof(ITestService));

            Should.NotThrow(() =>
                WcfClientProxy.Create<ITestService>(c =>
                    c.SetEndpoint(new ServiceEndpoint(contractDescription, this.TestServer.Binding, GetAddress<ITestService>()))));
        }

        [Fact]
        public async Task CreatingAsyncProxy_WithServiceEndpoint_CanCallAsyncMethod()
        {
            ContractDescription contractDescription = ContractDescription.GetContract(typeof(ITestService));

            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>(c =>
                c.SetEndpoint(new ServiceEndpoint(contractDescription, this.TestServer.Binding, GetAddress<ITestService>())));

            var response = await proxy.CallAsync(m => m.Echo("test"));

            response.ShouldBe("test");
        }

        [Fact, Description("Github issue #19.")]
        public void CreatingProxy_TrailingSlashOnNamespace()
        {
            var proxy = GenerateProxy<ITrailingSlashOnNamespaceService>();

            var result = proxy.Echo("hello");

            result.ShouldBe("hello");
        }

        [Fact, Description("Github issue #22")]
        public async Task AsyncMethod_FromSyncMethodWithFaultContract_CanBeCalled()
        {
            var proxy = GenerateAsyncProxy<ICustomAttributeService>();

            var result = await proxy.CallAsync(m => m.FaultMethod("hello"));

            result.ShouldBe("hello");
        }

        [Fact, Description("Github issue #22")]
        public async Task AsyncMethod_FromSyncMethodWithKnownTypeAttribute_CanBeCalled()
        {
            var proxy = GenerateAsyncProxy<ICustomAttributeService>();

            var result = await proxy.CallAsync(m => m.KnownTypeMethod("hello"));

            result.ShouldBe("hello");
        }

        [Fact]
        public void Proxy_ReturnsExpectedValue_WhenCallingService()
        {
            var proxy = GenerateProxy<ITestService>();

            var result = proxy.Echo("hello");
            result.ShouldBe("hello");
        }

        [Fact]
        public void Proxy_CanCallVoidMethod()
        {
            var proxy = GenerateProxy<ITestService>();

            proxy.VoidMethod("good");
        }

        [Fact, Description("github issue #12")]
        public void Proxy_CanCallServiceMethod_ThatReturnsNull()
        {
            var proxy = GenerateProxy<ITestService>();

            string response = proxy.Echo(null);

            response.ShouldBeNull();
        }

        [Fact, Description("github issue #12")]
        public async Task AsyncProxy_CanCallServiceMethod_ThatReturnsNull()
        {
            var proxy = GenerateAsyncProxy<ITestService>();

            string response = await proxy.CallAsync(m => m.Echo(null));

            response.ShouldBeNull();
        }

        [Fact]
        public void MultipleProxies_ReturnExpectedValues_WhenCallingServices()
        {
            var proxy1 = GenerateProxy<ITestService>();
            var proxy2 = GenerateProxy<ITrailingSlashOnNamespaceService>();

            proxy1.Echo("service 1").ShouldBe("service 1");
            proxy2.Echo("service 2").ShouldBe("service 2");
        }

        [Fact]
        public void Proxy_RecoversFromFaultedState_WhenCallingSimpleMethod()
        {
            var proxy = GenerateProxy<ITestService>();

            // Will fault the channel
            Should.Throw<Exception>(() => proxy.UnhandledExceptionOnFirstCallThenEcho("hello world"));
            proxy.UnhandledExceptionOnFirstCallThenEcho("hello world").ShouldBe("hello world");
        }

        [Fact]
        public void Proxy_RecoversFromFaultedState_WhenCallingComplexTypeMethod()
        {
            var proxy = GenerateProxy<ITestService>();

            var response = new Response { StatusCode = 123 };

            Should.Throw<Exception>(() => proxy.UnhandledExceptionOnFirstCall_Complex(new Request(), response));

            proxy.UnhandledExceptionOnFirstCall_Complex(new Request(), response).StatusCode.ShouldBe(response.StatusCode);
        }

        [Fact]
        public void Proxy_RecoversFromFaultedState_WhenCallingMultipleParameterComplexTypeMethod()
        {
            var proxy = GenerateProxy<ITestService>();

            var response = new Response { StatusCode = 123 };

            Should.Throw<Exception>(() =>
                proxy.UnhandledExceptionOnFirstCall_ComplexMulti("ok", new Request(), response));

            proxy.UnhandledExceptionOnFirstCall_ComplexMulti("ok", new Request(), response).StatusCode.ShouldBe(response.StatusCode);
        }

        [Fact]
        public void Proxy_CanBeGeneratedForInheritingServiceInterface()
        {
            var proxy = GenerateProxy<IChildService>();
            proxy.ChildMethod("hello").ShouldBe("hello");
        }

        [Fact, Description("A call made with no retries should not throw the WcfRetryFailedException")]
        public void Proxy_ConfiguredWithNoRetries_CallsServiceOnce_AndThrowsActualException()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.MaximumRetries(0);
                c.RetryOnException<FaultException>();
            });

            Should.Throw<FaultException>(() => proxy.FaultException());
        }

        [Fact]
        public void Proxy_ConfiguredWithAtLeastOnRetry_CallsServiceMultipleTimes_AndThrowsWcfRetryFailedException()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.MaximumRetries(1);
                c.RetryOnException<FaultException>();
            });

            Should.Throw<WcfRetryFailedException>(() => proxy.FaultException());
        }

        [Fact]
        public void Proxy_ConfiguredWithAtLeastOnRetry_CallsServiceMultipleTimes_AndThrowsCustomRetryFailureException()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.MaximumRetries(1);
                c.RetryOnException<FaultException>();
                c.RetryFailureExceptionFactory((attempts, exception, info) =>
                {
                    string message = $"Failed call to {info.MethodName} {attempts} times";
                    return new CustomFailureException(message, exception);
                });
            });

            Should.Throw<CustomFailureException>(() => proxy.FaultException());
        }

        public class CustomFailureException : Exception
        {
            public CustomFailureException(string message, Exception innerException) : base(message, innerException)
            { }
        }

        #region Out Parameter Support

        [Fact]
        public void Proxy_CanBeGeneratedForOperationWithSingleOutParameter()
        {
            byte[] expectedOutParam = { 0x01 };

            var proxy = GenerateProxy<IOutParamTestService>();

            int result = proxy.SingleOutParam(out var outParam);

            result.ShouldBe(1);
            outParam.ShouldBe(expectedOutParam);
        }

        [Fact]
        public void Proxy_CanBeGeneratedForOperationWithMultipleOutParameters()
        {
            byte[] expectedOut1Value = { 0x01 };
            string expectedOut2Value = "hello world";

            var proxy = GenerateProxy<IOutParamTestService>();

            int result = proxy.MultipleOutParams(out var out1Value, out var out2Value);

            result.ShouldBe(1);
            out1Value.ShouldBe(expectedOut1Value);
            out2Value.ShouldBe(expectedOut2Value);
        }

        #if NETFULL

        [Fact, Description("Currently fails for .NET Core cases. Not sure why.")]
        public void Proxy_CanBeGeneratedForOperationWithMixedInputAndOutputParams()
        {
            var proxy = GenerateProxy<IOutParamTestService>();

            int result = proxy.MixedParams(1, out var out1Value, "test");

            result.ShouldBe(1);
            out1Value.ShouldBe(25);
        }

        #endif
    
        #endregion

        [Fact]
        public void Proxy_CanBeUsedWithOneWayOperations()
        {
            var proxy = GenerateProxy<ITestService>();

            proxy.OneWay("test");
        }

        #region OnBeforeInvoke and OnAfterInvoke support

        [Fact]
        public void Proxy_OnBeforeInvoke_IsFired()
        {
            bool fired = false;
            var proxy = GenerateProxy<ITestService>(c => { c.OnBeforeInvoke += (sender, args) => fired = true; });

            proxy.VoidMethod("test");
            fired.ShouldBeTrue();
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_Multiple_AreFired()
        {
            bool fired1 = false,
                fired2 = false;

            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.OnBeforeInvoke += (sender, args) => fired1 = true;
                c.OnBeforeInvoke += (sender, args) => fired2 = true;
            });

            proxy.VoidMethod("test");

            fired1.ShouldBeTrue();
            fired2.ShouldBeTrue();
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_IfHandlerRemoved_NotFired()
        {
            bool fired = false;
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) => fired = true;
                c.OnBeforeInvoke += handler;
                c.OnBeforeInvoke -= handler;
            });

            proxy.VoidMethod("test");

            fired.ShouldBeFalse();
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_ArgumentsSetCorrectly()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.IsRetry.ShouldBeFalse("IsRetry is not set correctly");
                    args.RetryCounter.ShouldBe(0, "RetryCounter is not set correctly");
                    args.ServiceType.ShouldBe(typeof(ITestService), "ServiceType is not set correctly");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.VoidMethod("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.True(false, "OnBeforeInvoke not called");
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_IfRetry_FiredManyTimes()
        {
            var resetEvent = new AutoResetEvent(false);

            int fireCount = 0;
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.MaximumRetries(10);
                c.RetryOnException<FaultException<ExceptionDetail>>();

                OnInvokeHandler handler = (sender, args) =>
                {
                    fireCount++;
                    args.IsRetry.ShouldBe(fireCount > 1, "IsRetry is not set correctly");
                    args.RetryCounter.ShouldBe(fireCount - 1, "RetryCounter is not set correctly");
                    args.ServiceType.ShouldBe(typeof(ITestService), "ServiceType is not set correctly");

                    if (fireCount >= 2)
                        resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.UnhandledExceptionOnFirstCallThenEcho("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.True(false, "OnBeforeInvoke probably not called");

            fireCount.ShouldBe(2, "Not called two times!");
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_InvokeInfo_IsSet()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.ShouldNotBeNull("InvokeInfo is null when it should be set");

                    resetEvent.Set();
                };
                c.OnBeforeInvoke += handler;
            });

            proxy.Echo("test");

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly()
        {
            var request = new Request { RequestMessage = "message" };

            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.ComplexMulti), "InvokeInfo.MethodName is not set correctly");
                    // parameters
                    args.InvokeInfo.Parameters.Length.ShouldBe(3, "InvokeInfo.Parameters length is incorrect");
                    args.InvokeInfo.Parameters[0].ShouldBe("test", "InvokeInfo.Parameters[0] is not set correctly");
                    args.InvokeInfo.Parameters[1].ShouldBe(request, "InvokeInfo.Parameters[1] is not set correctly");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.ComplexMulti("test", request);

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly_NoParameters()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.VoidMethodNoParameters), "InvokeInfo.MethodName is not set correctly");
                    args.InvokeInfo.Parameters.Length.ShouldBe(0, "InvokeInfo.Parameters length is incorrect");

                    resetEvent.Set();
                };
                c.OnBeforeInvoke += handler;
            });

            proxy.VoidMethodNoParameters();

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly_IntParameter()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.VoidMethodIntParameter), "InvokeInfo.MethodName is not set correctly");
                    args.InvokeInfo.Parameters.Length.ShouldBe(1, "InvokeInfo.Parameters length is incorrect");
                    args.InvokeInfo.Parameters[0].ShouldBe(1337, "InvokeInfo.Parameters[0] is not set correctly");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.VoidMethodIntParameter(1337);

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Fact]
        public void Proxy_OnBeforeInvoke_ReturnValue_Throws()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.MethodHasReturnValue.ShouldBeFalse("InvokeInfo.MethodHasReturnValue is not set correctly");
                    Should.Throw<InvalidOperationException>(delegate
                    {
                        var x = args.InvokeInfo.ReturnValue;
                    }, "InvokeInfo.ReturnValue did not throw!");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.Echo("test");

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Fact]
        public void Proxy_OnAfterInvoke_IsFired()
        {
            bool fired = false;
            var proxy = GenerateProxy<ITestService>(c => { c.OnAfterInvoke += (sender, args) => fired = true; });

            proxy.VoidMethod("test");

            fired.ShouldBeTrue();
        }

        [Fact]
        public void Proxy_OnAfterInvoke_Multiple_AreFired()
        {
            bool fired1 = false;
            bool fired2 = false;
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.OnAfterInvoke += (sender, args) => fired1 = true;
                c.OnAfterInvoke += (sender, args) => fired2 = true;
            });

            proxy.VoidMethod("test");

            fired1.ShouldBeTrue();
            fired2.ShouldBeTrue();
        }

        [Fact]
        public void Proxy_OnAfterInvoke_IfHandlerRemoved_IsNotFired()
        {
            bool fired = false;
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) => fired = true;
                c.OnAfterInvoke += handler;
                c.OnAfterInvoke -= handler;
            });

            proxy.VoidMethod("test");

            fired.ShouldBeFalse();
        }

        [Fact]
        public void Proxy_OnAfterInvoke_ArgumentsSetCorrectly()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    args.IsRetry.ShouldBeFalse("IsRetry is not set correctly");
                    args.RetryCounter.ShouldBe(0, "RetryCounter is not set correctly");
                    args.ServiceType.ShouldBe(typeof(ITestService), "ServiceType is not set correctly");

                    resetEvent.Set();
                };

                c.OnAfterInvoke += handler;
            });

            proxy.VoidMethod("test");

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Fact]
        public void Proxy_OnAfterInvoke_IfException_IsNotFired()
        {
            bool fired = false;
            var proxy = GenerateProxy<ITestService>(c => { c.OnAfterInvoke += (sender, args) => fired = true; });

            try
            {
                proxy.UnhandledException();
            }
            catch
            { }

            fired.ShouldBeFalse("OnAfterInvoke was called when it should not have been!");
        }

        [Fact]
        public void Proxy_OnAfterInvoke_IfExceptionAndIfRetryCountUsedUp_IsNotFired()
        {
            int attempts = 0; // number of times method has been attempted to be called
            bool fired = false; // true if OnAfterInvoke event was fired
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.MaximumRetries(5);
                c.RetryOnException<FaultException<ExceptionDetail>>();
                c.SetDelayPolicy(() => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(10)));
                c.OnBeforeInvoke += (sender, args) => attempts++;
                c.OnAfterInvoke += (sender, args) => fired = true;
            });

            try
            {
                proxy.UnhandledException();
            }
            catch
            { }

            attempts.ShouldBe(6, "Assumption failed: Should attempt to call service method 6 times");
            fired.ShouldBeFalse("OnAfterInvoke was called when it should not have been!");
        }

        [Fact]
        public void Proxy_OnAfterInvoke_InvokeInfo_SetCorrectly()
        {
            Request request = new Request { RequestMessage = "message" };

            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.ComplexMulti), "InvokeInfo.MethodName is not set correctly");
                    // parameters
                    args.InvokeInfo.Parameters.Length.ShouldBe(3, "InvokeInfo.Parameters length is incorrect");
                    args.InvokeInfo.Parameters[0].ShouldBe("test", "InvokeInfo.Parameters[0] is not set correctly");
                    args.InvokeInfo.Parameters[1].ShouldBe(request, "InvokeInfo.Parameters[1] is not set correctly");

                    resetEvent.Set();
                };

                c.OnAfterInvoke += handler;
            });

            proxy.ComplexMulti("test", request);

            resetEvent.WaitOrFail("OnAfterInvoke not fired");
        }

        [Fact]
        public void Proxy_OnAfterInvoke_ReturnValue_IsSetCorrectly()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.MethodHasReturnValue.ShouldBeTrue("InvokeInfo.MethodHasReturnValue is not set correctly");
                    args.InvokeInfo.ReturnValue.ShouldBe("retval", "InvokeInfo.ReturnValue is not set correctly");

                    resetEvent.Set();
                };
                c.OnAfterInvoke += handler;
            });

            proxy.Echo("retval");

            resetEvent.WaitOrFail("OnAfterInvoke not fired");
        }

        [Fact]
        public void Proxy_OnAfterInvoke_ReturnValue_ForValueTypeMethods_IsSetCorrectly()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.MethodHasReturnValue.ShouldBeTrue("InvokeInfo.MethodHasReturnValue is not set correctly");
                    args.InvokeInfo.ReturnValue.ShouldBe(1337, "InvokeInfo.ReturnValue is not set correctly");

                    resetEvent.Set();
                };

                c.OnAfterInvoke += handler;
            });

            proxy.EchoInt(1337);

            resetEvent.WaitOrFail("OnAfterInvoke not fired");
        }

        [Fact]
        public void Proxy_OnAfterInvoke_ReturnValue_ThrowsForVoidMethods()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.MethodHasReturnValue.ShouldBeFalse("InvokeInfo.MethodHasReturnValue is not set correctly");
                    Should.Throw<InvalidOperationException>(delegate
                    {
                        var x = args.InvokeInfo.ReturnValue;
                    }, "InvokeInfo.ReturnValue did not throw!");

                    resetEvent.Set();
                };

                c.OnAfterInvoke += handler;
            });

            proxy.VoidMethod("test");

            resetEvent.WaitOrFail("OnAfterInvoke not fired");
        }

        #region AsyncProxy

        [Fact]
        public async Task AsyncProxy_OnBeforeInvoke_IsFired()
        {
            bool fired = false;
            var proxy = GenerateAsyncProxy<ITestService>(c => { c.OnBeforeInvoke += (sender, args) => fired = true; });

            await proxy.CallAsync(m => m.VoidMethod("test"));

            fired.ShouldBeTrue();
        }

        [Fact]
        public async Task AsyncProxy_OnBeforeInvoke_ArgumentsSetCorrectly()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.IsRetry.ShouldBeFalse("IsRetry is not set correctly");
                    args.RetryCounter.ShouldBe(0, "RetryCounter is not set correctly");
                    args.ServiceType.ShouldBe(typeof(ITestService), "ServiceType is not set correctly");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            await proxy.CallAsync(m => m.VoidMethod("test"));

            resetEvent.WaitOrFail("OnBeforeInvoke hasn't been called");
            ;
        }


        [Fact]
        public async Task AsyncProxy_OnBeforeInvoke_IfRetry_FiredManyTimes()
        {
            var resetEvent = new AutoResetEvent(false);
            int fireCount = 0;
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.MaximumRetries(10);
                c.RetryOnException<FaultException>();
                c.RetryOnException<FaultException<ExceptionDetail>>();

                OnInvokeHandler handler = (sender, args) =>
                {
                    fireCount++;
                    args.IsRetry.ShouldBe(fireCount > 1, "IsRetry is not set correctly");
                    args.RetryCounter.ShouldBe(fireCount - 1, "RetryCounter is not set correctly");
                    args.ServiceType.ShouldBe(typeof(ITestService), "ServiceType is not set correctly");

                    if (fireCount >= 2)
                        resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            await proxy.CallAsync(m => m.UnhandledExceptionOnFirstCallThenEcho("test"));

            resetEvent.WaitOrFail("OnBeforeInvoke probably not called");

            fireCount.ShouldBe(2, "Not called three times!");
        }

        [Fact]
        public async Task AsyncProxy_OnBeforeInvoke_InvokeInfo_IsSet()
        {
            var resetEvent = new AutoResetEvent(false);

            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.ShouldNotBeNull("InvokeInfo is null when it should be set");
                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            await proxy.CallAsync(m => m.Echo("test"));

            resetEvent.WaitOrFail("OnBeforeInvoke not called");
        }

        [Fact]
        public async Task AsyncProxy_OnAfterInvoke_IsFired()
        {
            bool fired = false;
            var proxy = GenerateAsyncProxy<ITestService>(c => { c.OnAfterInvoke += (sender, args) => fired = true; });

            await proxy.CallAsync(m => m.VoidMethod("test"));

            fired.ShouldBeTrue();
        }


        [Fact]
        public async Task AsyncProxy_OnAfterInvoke_ArgumentsSetCorrectly()
        {   
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.IsRetry.ShouldBeFalse("IsRetry is not set correctly");
                    args.RetryCounter.ShouldBe(0, "RetryCounter is not set correctly");
                    args.ServiceType.ShouldBe(typeof(ITestService), "ServiceType is not set correctly");

                    resetEvent.Set();
                };
                c.OnAfterInvoke += handler;
            });

            await proxy.CallAsync(m => m.VoidMethod("test"));

            resetEvent.WaitOrFail("OnAfterInvoke hasn't been called");
        }

        [Fact]
        public async Task AsyncProxy_OnAfterInvoke_InvokeInfo_IsSet()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    args.InvokeInfo.ShouldNotBeNull("InvokeInfo is null when it should be set");
                    resetEvent.Set();
                };
                c.OnAfterInvoke += handler;
            });
            
            await proxy.CallAsync(m => m.Echo("test"));

            resetEvent.WaitOrFail("OnAfterInvoke not called");
        }


        #endregion

        #endregion


        #region OnCallBegin and OnCallSuccess support

        [Fact]
        public void Proxy_OnCallBegin_IsFired()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.OnCallBegin += (invoker, args) =>
                {
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.Echo));
                    
                    resetEvent.Set();
                };
            });

            proxy.Echo("test");

            resetEvent.WaitOrFail("OnCallBegin was not triggered");
        }

        [Fact]
        public void Proxy_OnCallSuccess_IsFired()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.OnCallSuccess += (invoker, args) =>
                {
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.Echo));
                    args.InvokeInfo.ReturnValue.ShouldBe("test");
                    args.CallDuration.ShouldBeGreaterThan(TimeSpan.MinValue);

                    resetEvent.Set();
                };
            });

            proxy.Echo("test");

            resetEvent.WaitOrFail("OnCallSuccess was not triggered");
        }

        #region AsyncProxy

        [Fact]
        public async Task AsyncProxy_OnCallBegin_IsFired()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.OnCallBegin += (invoker, args) =>
                {
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.Echo) + "Async");
                    
                    resetEvent.Set();
                };
            });

            await proxy.CallAsync(m => m.Echo("test"));

            resetEvent.WaitOrFail("OnCallBegin was not triggered");
        }

        [Fact]
        public async Task AsyncProxy_OnCallSuccess_IsFired()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.OnCallSuccess += (invoker, args) =>
                {
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.Echo) + "Async");
                    args.InvokeInfo.ReturnValue.ShouldBe("test");
                    args.CallDuration.ShouldBeGreaterThan(TimeSpan.MinValue);

                    resetEvent.Set();
                };
            });

            await proxy.CallAsync(m => m.Echo("test"));

            resetEvent.WaitOrFail("OnCallSuccess was not triggered");
        }

        #endregion

        #endregion

        #region OnException support

        [Fact]
        public void Proxy_OnException_NoException_NotFired()
        {
            bool hasFired = false;
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.OnException += (sender, args) => hasFired = true;
            });

            proxy.VoidMethod("test");

            hasFired.ShouldBeFalse();
        }

        [Fact]
        public void Proxy_OnException_NoHandler_Compatibility()
        {
            var proxy = GenerateProxy<ITestService>();
            Should.Throw<FaultException>(() => proxy.FaultException());
        }
        
        [Fact]
        public void Proxy_OnException_IsFired()
        {
            bool hasFired = false;
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.OnException += (sender, args) => hasFired = true;
            });

            Should.Throw<FaultException>(() => proxy.FaultException());
            hasFired.ShouldBeTrue();
        }

        [Fact]
        public void Proxy_OnException_FiresOnEveryRetry()
        {
            int fireCount = 0;
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.MaximumRetries(5);
                c.RetryOnException<FaultException>();
                c.SetDelayPolicy(() => new ConstantDelayPolicy(TimeSpan.FromSeconds(0)));
                c.OnException += (sender, args) => fireCount++;
            });

            Should.Throw<Exception>(() => proxy.FaultException());
            fireCount.ShouldBe(6);
        }

        [Fact]
        public void Proxy_OnException_MultipleHandlersAreFired()
        {
            bool hasFired1 = false;
            bool hasFired2 = false;
            bool hasFired3 = false;

            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.OnException += (sender, args) => hasFired1 = true;
                c.OnException += (sender, args) => hasFired2 = true;
                c.OnException += (sender, args) => hasFired3 = true;
            });

            Should.Throw<FaultException>(() => proxy.FaultException());

            hasFired1.ShouldBeTrue();
            hasFired2.ShouldBeTrue();
            hasFired3.ShouldBeTrue();
        }

        [Fact]
        public void Proxy_OnException_InformationSetCorrectly()
        {
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.OnException += (sender, args) =>
                {
                    args.Exception.ShouldBeOfType<FaultException>();
                    args.InvokeInfo.MethodName.ShouldBe(nameof(ITestService.FaultException), "InvokeInfo.MethodName");
                    args.ServiceType.ShouldBe(typeof(ITestService), "ServiceType");

                    resetEvent.Set();
                };
            });

            Should.Throw<FaultException>(() => proxy.FaultException());

            resetEvent.WaitOrFail("OnException not fired");
        }

        #region AsyncProxy

        [Fact]
        public async Task AsyncProxy_OnException_NoHandler_Compatibility()
        {
            var proxy = GenerateAsyncProxy<ITestService>();

            await Should.ThrowAsync<FaultException>(() => proxy.CallAsync(m => m.FaultException()));
        }

        [Fact]
        public async Task AsyncProxy_OnException_IsFired()
        {
            bool hasFired = false;
            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.OnException += (sender, args) =>
                {
                    hasFired = true;
                    resetEvent.Set();
                };
            });

            await Should.ThrowAsync<FaultException>(() => proxy.CallAsync(m => m.FaultException()));
            
            resetEvent.WaitOrFail("OnException not fired");

            hasFired.ShouldBeTrue();
        }

        #endregion

        #endregion

        #region ChannelFactory support

#if NETFULL

        [Fact]
        public void Proxy_ChannelFactory_IfNotConfigured_UsesDefaultEndpoint()
        {
            WcfClientProxy.Create<ITestServiceSingleEndpointConfig>(c =>
            {
                // assert that the endpoint url is read from app.config
                c.ChannelFactory.Endpoint.Address.Uri.ShouldBe(new Uri("http://localhost:23456/TestService2"));
            });
        }

#endif
        
        [Fact]
        public void Proxy_ChannelFactory_UsesConfiguredEndpoint()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.SetEndpoint(new BasicHttpBinding(), new EndpointAddress("http://localhost:23456/SomeOtherTestServicUrl"));
                // assert that the endpoint is the same
                c.ChannelFactory.Endpoint.Address.Uri.ShouldBe(new Uri("http://localhost:23456/SomeOtherTestServicUrl"));
            });
        }

        #endregion

        #region HandleRequestArgument

        [Fact]
        public void HandleRequestArgument_ModifiesComplexRequest_BeforeSendingToService()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleRequestArgument<Request>(
                    where: (arg, param) => arg == null,
                    handler: arg => new Request
                    {
                        RequestMessage = "default message"
                    });
            });

            var defaultResponse = proxy.Complex(null);
            defaultResponse.ResponseMessage.ShouldBe("default message");
            
            var response = proxy.Complex(new Request { RequestMessage = "set" });
            response.ResponseMessage.ShouldBe("set");
        }

        [Fact]
        public void HandleRequestArgument_MatchesArgumentsOfSameType_BasedOnParameterName()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {               
                c.HandleRequestArgument<string>(
                    where: (arg, paramName) => paramName == "input",
                    handler: arg => "always input");
                
                c.HandleRequestArgument<string>(
                    where: (arg, paramName) => paramName == "secondInput",
                    handler: arg => "always two");
            });

            var response = proxy.Echo("first argument", "second argument");

            response.ShouldBe("always input always two");
        }

        [Fact]
        public void HandleRequestArgument_MatchesArgumentsByBaseTypes()
        {
            int handleRequestArgumentCounter = 0;

            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleRequestArgument<object>(
                    handler: arg =>
                    {
                        handleRequestArgumentCounter++;
                    });
            });

            proxy.EchoMixed("first argument", 100);

            handleRequestArgumentCounter.ShouldBe(2);
        }
        
        #endregion

        #region HandleResponse
//
        [Fact]
        public void HandleResponse_CanChangeResponse_ForSimpleResponseType()
        {
            const string expectedInput = "test";

            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r => "hello: " + r);
            });

            var response = proxy.Echo(expectedInput);

            response.ShouldBe("hello: " + expectedInput);
        }

        [Fact]
        public void HandleResponse_ActionWithPredicate_CanInspectResponse_WithoutReturning()
        {
            const string expectedInput = "test";

            var resetEvent = new AutoResetEvent(false);
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r =>
                {
                    r.ShouldBe(expectedInput);
                    resetEvent.Set();
                });
            });

            var response = proxy.Echo(expectedInput);
            response.ShouldBe(expectedInput);

            resetEvent.WaitOrFail("Callback not fired");
        }

        [Fact]
        public void HandleResponse_ActionWithoutPredicate_CanInspectResponse_WithoutReturning()
        {
            var resetEvent = new AutoResetEvent(false);

            const string expectedInput = "test";

            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleResponse<string>(handler: r =>
                {
                    r.ShouldBe(expectedInput);
                    resetEvent.Set();
                });
            });

            var response = proxy.Echo(expectedInput);

            response.ShouldBe(expectedInput);

            resetEvent.WaitOrFail("Callback not fired");
        }

        [Fact]
        public void HandleResponse_CanChangeResponse_ForComplexResponseType()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleResponse<Response>(r =>
                {
                    r.ResponseMessage = "hello: " + r.ResponseMessage;
                    return r;
                });
            });

            var request = new Request { RequestMessage = "test" };
            var response = proxy.Complex(request);

            response.ResponseMessage.ShouldBe("hello: test");
        }    
    
        [Fact]
        public void HandleResponse_CanChangeResponse_ForComplexResponse_InterfaceType()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {               
                // Rewrite responses with status code of 100
                c.HandleResponse<IResponseStatus>(r =>
                {
                    if (r.StatusCode == 100)
                    {
                        return new Response
                        {
                            ResponseMessage = "error",
                            StatusCode = 1
                        };
                    }
                    
                    return r;
                });
            });

            var request = new Request();
            var response = proxy.Complex(request, new Response { StatusCode = 100 });

            response.ResponseMessage.ShouldBe("error");
            response.StatusCode.ShouldBe(1);
        }

        [Fact]
        public void HandleResponse_CanThrowException()
        {
            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r =>
                {
                    throw new Exception(r);
                });
            });

            Should.NotThrow(() => proxy.Echo("hello"));
            
            var ex = Should.Throw<Exception>(() => proxy.Echo("test"));
            ex.Message.ShouldBe("test");
        }

        [Fact]
        public void HandleResponse_MultipleHandlersCanBeRunOnResponse()
        {
            var countdownEvent = new CountdownEvent(2);

            var serviceResponse = new Response()
            {
                ResponseMessage = "message",
                StatusCode = 100
            };

            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleResponse<Response>(r => r.StatusCode == serviceResponse.StatusCode, r =>
                {
                    countdownEvent.Signal();
                });

                c.HandleResponse<Response>(r => r.ResponseMessage == serviceResponse.ResponseMessage, r =>
                {
                    countdownEvent.Signal();
                });
            });

            var response = proxy.Complex(new Request(), serviceResponse);
            
            if (!countdownEvent.Wait(250))
                Assert.True(false, "Expected both callbacks to fire");
        }

        [Fact]
        public void HandleResponse_MultipleHandlersCanBeRunOnResponse_WhereHandlersAreInheritingTypes()
        {
            var countdownEvent = new CountdownEvent(3);

            var serviceResponse = new Response()
            {
                ResponseMessage = "message",
                StatusCode = 100
            };

            var proxy = GenerateProxy<ITestService>(c =>
            {
                c.HandleResponse<object>(r =>
                {
                    countdownEvent.Signal();
                });

                c.HandleResponse<IResponseStatus>(r => r.StatusCode == serviceResponse.StatusCode, r =>
                {
                    countdownEvent.Signal();
                });

                c.HandleResponse<Response>(r => r.ResponseMessage == serviceResponse.ResponseMessage, r =>
                {
                    countdownEvent.Signal();
                });
            });

            var response = proxy.Complex(new Request(), serviceResponse);
            
            if (!countdownEvent.Wait(250))
                Assert.True(false, "Expected both callbacks to fire");
        }

        #region AsyncProxy

        [Fact]
        public async Task Async_HandleResponse_CanChangeResponse_ForSimpleResponseType()
        {
            const string expectedInput = "test";

            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r => "hello: " + r);
            });

            var response = await proxy.CallAsync(m => m.Echo(expectedInput));

            response.ShouldBe("hello: test");
        }
        
        [Fact]
        public async Task Async_HandleResponse_CanChangeResponse_ForComplexResponseType()
        {
            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.HandleResponse<Response>(r =>
                {
                    r.ResponseMessage = "hello: " + r.ResponseMessage;
                    return r;
                });
            });

            var request = new Request { RequestMessage = "test" };
            var response = await proxy.CallAsync(m => m.Complex(request));

            response.ResponseMessage.ShouldBe("hello: test");
        }    
    
        [Fact]
        public async Task Async_HandleResponse_CanChangeResponse_ForComplexResponse_InterfaceType()
        {
//            var service = Substitute.For<ITestService>();
//
//            service
//                .TestMethodComplex(Arg.Any<Request>())
//                .Returns(m => new Response
//                {
//                    ResponseMessage = m.Arg<Request>().RequestMessage,
//                    StatusCode = 100
//                });

            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {               
                // Rewrite responses with status code of 100
                c.HandleResponse<IResponseStatus>(r =>
                {
                    if (r.StatusCode == 100)
                    {
                        return new Response
                        {
                            ResponseMessage = "error",
                            StatusCode = 1
                        };
                    }
                    
                    return r;
                });
            });

            var request = new Request();
            var response = await proxy.CallAsync(m => m.Complex(request, new Response { StatusCode = 100 }));

            response.ResponseMessage.ShouldBe("error");
            response.StatusCode.ShouldBe(1);
        }

        [Fact]
        public async Task Async_HandleResponse_CanThrowException()
        {
            const string expectedInput = "test";

            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.HandleResponse<string>(
                    where: r => r.StartsWith("te"), 
                    handler: r => throw new Exception(r));
            });

            var exception = await Should.ThrowAsync<Exception>(() => proxy.CallAsync(m => m.Echo(expectedInput)));

            exception.Message.ShouldBe("test");
        }


        [Fact]
        public async Task Async_HandleResponse_ActionWithPredicate_CanInspectResponse_WithoutReturning()
        {
            var resetEvent = new AutoResetEvent(false);

            const string expectedInput = "test";

            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r =>
                {
                    r.ShouldBe(expectedInput);
                    resetEvent.Set();
                });
            });

            var response = await proxy.CallAsync(m => m.Echo(expectedInput));

            response.ShouldBe(expectedInput);

            resetEvent.WaitOrFail("Callback not fired");
        }

        [Fact]
        public async Task Async_HandleResponse_ActionWithoutPredicate_CanInspectResponse_WithoutReturning()
        {
            var resetEvent = new AutoResetEvent(false);

            const string expectedInput = "test";

            var proxy = GenerateAsyncProxy<ITestService>(c =>
            {
                c.HandleResponse<string>(handler: r =>
                {
                    r.ShouldBe(expectedInput);
                    resetEvent.Set();
                });
            });

            var response = await proxy.CallAsync(m => m.Echo(expectedInput));

            response.ShouldBe(expectedInput);

            resetEvent.WaitOrFail("Callback not fired");
        }

        #endregion

        #endregion

        #region Dynamic Async Invocation

        [Fact]
        public async Task Async_DynamicConversion_Proxy_ReturnsExpectedValue_WhenCallingGeneratedAsyncMethod()
        {
            var proxy = GenerateProxy<ITestService>();
            
            // ITestService does not define TestMethodAsync, it's generated at runtime
            string result = await ((dynamic) proxy).EchoAsync("good");

            result.ShouldBe("good");
        }

        [Fact]
        public async Task Async_DynamicConversion_Proxy_CanCallGeneratedAsyncVoidMethod()
        {
            var resetEvent = new AutoResetEvent(false);

            var proxy = GenerateProxy<ITestService>();
            
            // ITestService does not define VoidMethodAsync, it's generated at runtime
            await ((dynamic) proxy).VoidMethodAsync("good");
        }

        #endregion

        #region Better error messages tests

        [ServiceContract]
        private interface IPrivateTestService
        {
            [OperationContract]
            void TestMethod();
        }

        public interface INonServiceInterface
        {
            [OperationContract]
            void TestMethod();
        }

        [ServiceContract]
        public interface INoOperationsInterface
        {
            void NonOperationTestMethod();
        }

        [Fact]
        public void Proxy_GivesProperException_IfInterfaceNotPublic()
        {
            Should.Throw<InvalidOperationException>(delegate { WcfClientProxy.Create<IPrivateTestService>(); });
            // error message not checked here, but it should be quite readable
        }

        [Fact]
        public void Proxy_GivesProperException_IfNotServiceContract()
        {
            Should.Throw<InvalidOperationException>(delegate { WcfClientProxy.Create<INonServiceInterface>(); });
            // error message not checked here, but it should be quite readable
        }

        [Fact]
        public void Proxy_GivesProperException_IfZeroOperationContracts()
        {
            Should.Throw<InvalidOperationException>(delegate { WcfClientProxy.Create<INoOperationsInterface>(); });
            // error message not checked here, but it should be quite readable
        }

        #endregion
    }
}