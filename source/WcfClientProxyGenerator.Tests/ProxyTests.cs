﻿using System;
using System.Runtime.Remoting.Messaging;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using NUnit.Framework;
using WcfClientProxyGenerator.Policy;
using WcfClientProxyGenerator.Tests.Infrastructure;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class ProxyTests
    {
        [SetUp]
        public void Setup()
        {
            DynamicProxyAssembly.Initialize();
        }

        [Test] 
        [Description("Asserts that when no conguration is given in the Create proxy call, the endpoint config that matches the contract will be used")]
        public void CreatingProxy_WithNoConfigurator_AndSingleEndpointConfig_GetsDefaultClientConfiguration()
        {
            WcfClientProxy.Create<ITestServiceSingleEndpointConfig>();
        }

        [Test] 
        [Description("Asserts that when no conguration is given in the Create proxy call, and multiple endpoint configs for the contract exist, an exception is thrown")]
        public void CreatingProxy_WithNoConfigurator_AndMultipleEndpointConfigs_ThrowsException()
        {
            Assert.That(
                () => WcfClientProxy.Create<ITestService>(),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CreatingProxy_WithNoConfigurator_AndNoDefaultConfiguration_ThrowsException()
        {
            Assert.That(
                () => WcfClientProxy.Create<ITestService2>(), 
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CreatingProxy_WithEndpointConfigurationName_ThatExists_CreatesProxy()
        {
            WcfClientProxy.Create<ITestService>("ITestService2");
        }

        [Test]
        public void CreatingProxy_WithEndpointConfigurationName_ThatDoesNotExist_ThrowsException()
        {
            Assert.That(
                () => WcfClientProxy.Create<ITestService>("DoesNotExist"),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CreatingProxy_WithServiceEndpoint_CreatesProxy()
        {
            var service = Substitute.For<ITestService>();
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(service);

            ContractDescription contractDescription = ContractDescription.GetContract(typeof(ITestService));
            Assert.DoesNotThrow(() => 
                WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(new ServiceEndpoint(contractDescription, serviceHost.Binding, serviceHost.EndpointAddress)))
            );
        }

        [Test]
        public void CreatingProxy_WithServiceEndpoint_CreatesProxy_Dispose()
        {
            var service = Substitute.For<ITestService>();
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(service);
            ContractDescription contractDescription = ContractDescription.GetContract(typeof(ITestService));
            ITestService cl = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(new ServiceEndpoint(
                contractDescription,
                serviceHost.Binding, serviceHost.EndpointAddress)));
            Assert.DoesNotThrow(() =>
            {
                using ((IDisposable)cl)
                {
                }
            });
        }
        [Test]
        public async Task CreatingAsyncProxy_WithServiceEndpoint_CanCallAsyncMethod()
        {
            var service = Substitute.For<ITestService>();

            service
                .IntMethod()
                .Returns(1);

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(service);

            ContractDescription contractDescription = ContractDescription.GetContract(typeof(ITestService));

            var proxy = WcfClientProxy.CreateAsyncProxy<ITestService>(c =>
                c.SetEndpoint(new ServiceEndpoint(contractDescription, serviceHost.Binding, serviceHost.EndpointAddress)));

            var response = await proxy.CallAsync(m => m.IntMethod());

            Assert.That(response, Is.EqualTo(1));
        }

        [Test, Description("Github issue #19.")]
        public void CreatingProxy_TrailingSlashOnNamespace()
        {
            var service = Substitute.For<ITrailingSlashOnNamespaceService>();

            service
                .Echo(Arg.Any<string>())
                .Returns(m => m.Arg<string>());

            var proxy = service.StartHostAndProxy();

            var result = proxy.Echo("hello");

            Assert.That(result, Is.EqualTo("hello"));
        }

        [Test, Description("Github issue #22")]
        public async Task AsyncMethod_FromSyncMethodWithFaultContract_CanBeCalled()
        {
            var service = Substitute.For<ICustomAttributeService>();

            service
                .FaultMethod(Arg.Any<string>())
                .Returns("hello");

            var proxy = service.StartHostAndAsyncProxy();

            var result = await proxy.CallAsync(m => m.FaultMethod(""));

            Assert.That(result, Is.EqualTo("hello"));
        }

        [Test, Description("Github issue #22")]
        public async Task AsyncMethod_FromSyncMethodWithKnownTypeAttribute_CanBeCalled()
        {
            var service = Substitute.For<ICustomAttributeService>();

            service
                .KnownTypeMethod(Arg.Any<string>())
                .Returns("hello");

            var proxy = service.StartHostAndAsyncProxy();

            var result = await proxy.CallAsync(m => m.KnownTypeMethod(""));

            Assert.That(result, Is.EqualTo("hello"));
        }

        [Test]
        public void Proxy_ReturnsExpectedValue_WhenCallingService()
        {
            var service = Substitute.For<ITestService>();
            service
                .TestMethod("good")
                .Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(service);

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));
            
            var result = proxy.TestMethod("good");
            Assert.AreEqual("OK", result);
        }

        [Test]
        public void Proxy_CanCallVoidMethod()
        {
            var resetEvent = new AutoResetEvent(false);

            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("good"))
                .Do(x =>
                {
                    Assert.That(x.Arg<string>(), Is.EqualTo("good"));
                    resetEvent.Set();
                });

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(service);

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));
            proxy.VoidMethod("good");

            if (!resetEvent.WaitOne(300))
                Assert.Fail("Timeout occurred when waiting for callback");
        }

        [Test, Description("github issue #12")]
        public void Proxy_CanCallServiceMethod_ThatReturnsNull()
        {
            var service = Substitute.For<ITestService>();
            service
                .TestMethod(Arg.Any<string>())
                .ReturnsNull();

            var proxy = service.StartHostAndProxy();

            string response = proxy.TestMethod("input");

            Assert.That(response, Is.Null);
        }

        [Test, Description("github issue #12")]
        public async Task AsyncProxy_CanCallServiceMethod_ThatReturnsNull()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod(Arg.Any<string>())
                .ReturnsNull();

            var proxy = service.StartHostAndAsyncProxy();

            string response = await proxy.CallAsync(m => m.TestMethod("input"));

            Assert.That(response, Is.Null);
        }

        [Test]
        public void MultipleProxies_ReturnExpectedValues_WhenCallingServices()
        {
            var service1 = Substitute.For<ITestService>();
            service1.TestMethod("service1").Returns("OK from service 1");

            var service2 = Substitute.For<ITestService2>();
            service2.TestMethod("service2").Returns("OK from service 2");

            var proxy1 = service1.StartHostAndProxy();
            var proxy2 = service2.StartHostAndProxy();
            
            Assert.AreEqual("OK from service 1", proxy1.TestMethod("service1"));
            Assert.AreEqual("OK from service 2", proxy2.TestMethod("service2"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingSimpleMethod()
        {
            var service = Substitute.For<ITestService>();
            service.TestMethod("good").Returns("OK");
            service.TestMethod("bad").Throws<Exception>();

            var proxy = service.StartHostAndProxy();

            // Will fault the channel
            Assert.That(() => proxy.TestMethod("bad"), Throws.Exception);
            Assert.That(() => proxy.TestMethod("good"), Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingComplexTypeMethod()
        {
            var badRequest = new Request() { RequestMessage = "bad" };
            var goodRequest = new Request() { RequestMessage = "good" };

            var service = Substitute.For<ITestService>();
            service.TestMethodComplex(goodRequest).Returns(new Response { ResponseMessage = "OK" });
            service.TestMethodComplex(badRequest).Throws<Exception>();

            var proxy = service.StartHostAndProxy();

            // Will fault the channel
            Assert.That(() => proxy.TestMethodComplex(badRequest), Throws.Exception);
            Assert.That(() => proxy.TestMethodComplex(goodRequest).ResponseMessage, Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingMultipleParameterComplexTypeMethod()
        {
            var badRequest = new Request() { RequestMessage = "bad" };
            var goodRequest = new Request() { RequestMessage = "good" };

            var service = Substitute.For<ITestService>();
            service.TestMethodComplexMulti("good", goodRequest).Returns(new Response { ResponseMessage = "OK" });
            service.TestMethodComplexMulti("bad", badRequest).Throws<Exception>();

            var proxy = service.StartHostAndProxy();

            // Will fault the channel
            Assert.That(() => proxy.TestMethodComplexMulti("bad", badRequest), Throws.Exception);
            Assert.That(() => proxy.TestMethodComplexMulti("good", goodRequest).ResponseMessage, Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_CanBeGeneratedForInheritingServiceInterface()
        {
            var service = Substitute.For<ITestService>();
            var childService = Substitute.For<IChildService>();

            childService
                .ChildMethod(Arg.Any<string>())
                .Returns("OK");

            var proxy = childService.StartHostAndProxy();

            Assert.That(() => proxy.ChildMethod("test"), Is.EqualTo("OK"));
        }

        [Test, Description("A call made with no retries should not throw the WcfRetryFailedException")]
        public void Proxy_ConfiguredWithNoRetries_CallsServiceOnce_AndThrowsActualException()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            var proxy = service.StartHostAndProxy(c =>
            {
                c.MaximumRetries(0);
                c.RetryOnException<FaultException>();
            });

            Assert.That(() => proxy.VoidMethod("test"), Throws.TypeOf<FaultException>());

            service
                .Received(1)
                .VoidMethod("test");
        }

        [Test]
        public void Proxy_ConfiguredWithAtLeastOnRetry_CallsServiceMultipleTimes_AndThrowsWcfRetryFailedException()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());


            var proxy = service.StartHostAndProxy(c =>
            {
                c.MaximumRetries(1);
                c.RetryOnException<FaultException>();
            });

            Assert.That(() => proxy.VoidMethod("test"), Throws.TypeOf<WcfRetryFailedException>());

            service
                .Received(2)
                .VoidMethod("test");
        }

        [Test]
        public void Proxy_ConfiguredWithAtLeastOnRetry_CallsServiceMultipleTimes_AndThrowsCustomRetryFailureException()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            var proxy = service.StartHostAndProxy(c =>
            {
                c.MaximumRetries(1);
                c.RetryOnException<FaultException>();
                c.RetryFailureExceptionFactory((attempts, exception, info) =>
                {
                    string message = $"Failed call to {info.MethodName} {attempts} times";
                    return new CustomFailureException(message, exception);
                });
            });

            Assert.That(() => proxy.VoidMethod("test"), Throws.TypeOf<CustomFailureException>());

            service
                .Received(2)
                .VoidMethod("test");
        }

        public class CustomFailureException : Exception
        {
            public CustomFailureException(string message, Exception innerException) : base(message, innerException)
            {}
        }

        #region Out Parameter Support

        [Test]
        public void Proxy_CanBeGeneratedForOperationWithSingleOutParameter()
        {
            var service = Substitute.For<IOutParamTestService>();

            byte[] expectedOutParam = { 0x00, 0x01 };

            byte[] outPlaceholder;
            service
                .SingleOutParam(out outPlaceholder)
                .Returns(m =>
                {
                    m[0] = expectedOutParam;
                    return 1;
                });

            var proxy = service.StartHostAndProxy();

            byte[] outParam;
            int result = proxy.SingleOutParam(out outParam);

            Assert.That(result, Is.EqualTo(1));
            Assert.That(outParam, Is.EqualTo(expectedOutParam));
        }

        [Test]
        public void Proxy_CanBeGeneratedForOperationWithMultipleOutParameters()
        {
            var service = Substitute.For<IOutParamTestService>();           

            byte[] expectedOut1Value = { 0x00, 0x01 };
            string expectedOut2Value = "message";

            byte[] out1ValuePlaceholder;
            string out2ValuePlaceholder;
            service
                .MultipleOutParams(out out1ValuePlaceholder, out out2ValuePlaceholder)
                .Returns(m =>
                {
                    m[0] = expectedOut1Value;
                    m[1] = expectedOut2Value;
                    return 1;
                });

            var proxy = service.StartHostAndProxy();

            byte[] out1Value;
            string out2Value;
            int result = proxy.MultipleOutParams(out out1Value, out out2Value);

            Assert.That(result, Is.EqualTo(1));
            Assert.That(out1Value, Is.EqualTo(expectedOut1Value));
            Assert.That(out2Value, Is.EqualTo(expectedOut2Value));
        }

        [Test]
        public void Proxy_CanBeGeneratedForOperationWithMixedInputAndOutputParams()
        {
            var service = Substitute.For<IOutParamTestService>();           

            const int expectedOut1Value = 25;
            int value = 0;
            service
                .MixedParams(1, out value, "test")
                .Returns(m =>
                {
                    m[1] = expectedOut1Value;
                    return 1;
                });

            var proxy = service.StartHostAndProxy();

            int out1Value;
            int result = proxy.MixedParams(1, out out1Value, "test");

            Assert.That(result, Is.EqualTo(1));
            Assert.That(out1Value, Is.EqualTo(expectedOut1Value));
        }

        [Test]
        public void Proxy_CanBeUsedWithOneWayOperations()
        {
            var resetEvent = new AutoResetEvent(false);

            var service = Substitute.For<ITestService>();

            service
                .When(m => m.OneWay(Arg.Any<string>()))
                .Do(m =>
                {
                    Assert.That(m.Arg<string>(), Is.EqualTo("test"));
                    resetEvent.Set();
                });

            var proxy = service.StartHostAndProxy();

            proxy.OneWay("test");
            
            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("Callback not entered");
        }

        #endregion

        #region OnBeforeInvoke and OnAfterInvoke support

        [Test]
        public void Proxy_OnBeforeInvoke_IsFired()
        {
            var service = Substitute.For<ITestService>();
            
            bool fired = false;
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnBeforeInvoke += (sender, args) => fired = true;
            });

            proxy.VoidMethod("test");
            Assert.IsTrue(fired);
        }

        [Test]
        public void Proxy_OnBeforeInvoke_Multiple_AreFired()
        {
            var service = Substitute.For<ITestService>();
            
            bool fired1 = false, 
                 fired2 = false;

            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnBeforeInvoke += (sender, args) => fired1 = true;
                c.OnBeforeInvoke += (sender, args) => fired2 = true;
            });

            proxy.VoidMethod("test");

            Assert.IsTrue(fired1);
            Assert.IsTrue(fired2);
        }

        [Test]
        public void Proxy_OnBeforeInvoke_IfHandlerRemoved_NotFired()
        {
            var service = Substitute.For<ITestService>();
            
            bool fired = false;
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) => fired = true;
                c.OnBeforeInvoke += handler;
                c.OnBeforeInvoke -= handler;
            });

            proxy.VoidMethod("test");

            Assert.IsFalse(fired);
        }

        [Test]
        public void Proxy_OnBeforeInvoke_ArgumentsSetCorrectly()
        {
            var service = Substitute.For<ITestService>();
            
            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.AreEqual(false, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(0, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType is not set correctly");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.VoidMethod("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("OnBeforeInvoke not called");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_IfRetry_FiredManyTimes()
        {
            var resetEvent = new AutoResetEvent(false);

            // set up a service method that throws first two times, and completes on the third time
            var serviceSub = Substitute.For<IExceptionDetailService>();           
            var service = new ExceptionDetailService(serviceSub);

            int mockFireCount = 0;

            serviceSub
                .When(m => m.Method(Arg.Any<string>()))
                .Do(_ =>
                {
                    mockFireCount++;
                    if (mockFireCount < 3)
                        throw new Exception();
                });

            int fireCount = 0;
            var proxy = service.StartHostAndProxy<IExceptionDetailService>(c =>
            {
                c.MaximumRetries(10);
                c.RetryOnException<FaultException<ExceptionDetail>>();

                OnInvokeHandler handler = (sender, args) =>
                {
                    fireCount++;
                    Assert.AreEqual(fireCount > 1, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(fireCount - 1, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(IExceptionDetailService), args.ServiceType, "ServiceType is not set correctly");
                    
                    if (fireCount >= 2)
                        resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.Method("test");

            if (!resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
                Assert.Fail("OnBeforeInvoke probably not called");

            Assert.AreEqual(3, fireCount, "Not called three times!");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_InvokeInfo_IsSet()
        {
            var service = Substitute.For<ITestService>();

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.IsNotNull(args.InvokeInfo, "InvokeInfo is null when it should be set");

                    resetEvent.Set();
                };
                c.OnBeforeInvoke += handler;
            });

            proxy.TestMethod("test");

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly()
        {
            var service = Substitute.For<ITestService>();
            
            var request = new Request { RequestMessage = "message" };

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.AreEqual("TestMethodComplexMulti", args.InvokeInfo.MethodName, "InvokeInfo.MethodName is not set correctly");
                    // parameters
                    Assert.AreEqual(2, args.InvokeInfo.Parameters.Length, "InvokeInfo.Parameters length is incorrect");
                    Assert.AreEqual("test", args.InvokeInfo.Parameters[0], "InvokeInfo.Parameters[0] is not set correctly");
                    Assert.AreEqual(request, args.InvokeInfo.Parameters[1], "InvokeInfo.Parameters[1] is not set correctly");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;

            });

            proxy.TestMethodComplexMulti("test", request);

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly_NoParameters()
        {
            var service = Substitute.For<ITestService>();

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.AreEqual("VoidMethodNoParameters", args.InvokeInfo.MethodName, "InvokeInfo.MethodName is not set correctly");
                    Assert.AreEqual(0, args.InvokeInfo.Parameters.Length, "InvokeInfo.Parameters length is incorrect");

                    resetEvent.Set();
                };
                c.OnBeforeInvoke += handler;
            });

            proxy.VoidMethodNoParameters();

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly_IntParameter()
        {
            var service = Substitute.For<ITestService>();

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.AreEqual("VoidMethodIntParameter", args.InvokeInfo.MethodName, "InvokeInfo.MethodName is not set correctly");
                    Assert.AreEqual(1, args.InvokeInfo.Parameters.Length, "InvokeInfo.Parameters length is incorrect");
                    Assert.AreEqual(1337, args.InvokeInfo.Parameters[0], "InvokeInfo.Parameters[0] is not set correctly");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.VoidMethodIntParameter(1337);

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_ReturnValue_Throws()
        {
            var service = Substitute.For<ITestService>();
            
            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.IsFalse(args.InvokeInfo.MethodHasReturnValue, "InvokeInfo.MethodHasReturnValue is not set correctly");
                    Assert.Throws<InvalidOperationException>(delegate { var x = args.InvokeInfo.ReturnValue; }, "InvokeInfo.ReturnValue did not throw!");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            proxy.TestMethod("test");

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Test]
        public void Proxy_OnAfterInvoke_IsFired()
        {
            var service = Substitute.For<ITestService>();
            
            bool fired = false;
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnAfterInvoke += (sender, args) => fired = true;
            });

            proxy.VoidMethod("test");

            Assert.IsTrue(fired);
        }

        [Test]
        public void Proxy_OnAfterInvoke_Multiple_AreFired()
        {
            var service = Substitute.For<ITestService>();
            
            bool fired1 = false;
            bool fired2 = false;
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnAfterInvoke += (sender, args) => fired1 = true;
                c.OnAfterInvoke += (sender, args) => fired2 = true;
            });

            proxy.VoidMethod("test");

            Assert.IsTrue(fired1);
            Assert.IsTrue(fired2);
        }

        [Test]
        public void Proxy_OnAfterInvoke_IfHandlerRemoved_IsNotFired()
        {
            var service = Substitute.For<ITestService>();
            
            bool fired = false;
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) => fired = true;
                c.OnAfterInvoke += handler;
                c.OnAfterInvoke -= handler;
            });

            proxy.VoidMethod("test");

            Assert.IsFalse(fired);
        }

        [Test]
        public void Proxy_OnAfterInvoke_ArgumentsSetCorrectly()
        {
            var service = Substitute.For<ITestService>();

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.AreEqual(false, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(0, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType is not set correctly");

                    resetEvent.Set();
                };

                c.OnAfterInvoke += handler;
            });

            proxy.VoidMethod("test");

            resetEvent.WaitOrFail("OnBeforeInvoke not fired");
        }

        [Test]
        public void Proxy_OnAfterInvoke_IfException_IsNotFired()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod(Arg.Any<string>()))
                .Throw<Exception>();

            bool fired = false;
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnAfterInvoke += (sender, args) => fired = true;
            });

            try
            {
                proxy.VoidMethod("test");
            }
            catch { }

            Assert.IsFalse(fired, "OnAfterInvoke was called when it should not have been!");
        }

        [Test]
        public void Proxy_OnAfterInvoke_IfExceptionAndIfRetryCountUsedUp_IsNotFired()
        {
            var serviceSub = Substitute.For<IExceptionDetailService>();

            serviceSub
                .Method(Arg.Any<string>())
                .Throws<Exception>();

            var service = new ExceptionDetailService(serviceSub);

            int attempts = 0; // number of times method has been attempted to be called
            bool fired = false; // true if OnAfterInvoke event was fired
            var proxy = service.StartHostAndProxy<IExceptionDetailService>(c =>
            {
                c.MaximumRetries(5);
                c.RetryOnException<FaultException<ExceptionDetail>>();
                c.SetDelayPolicy(() => new ConstantDelayPolicy(TimeSpan.FromMilliseconds(10)));
                c.OnBeforeInvoke += (sender, args) => attempts++;
                c.OnAfterInvoke += (sender, args) => fired = true;
            });

            try
            {
                proxy.Method("test");
            }
            catch { }

            Assert.AreEqual(6, attempts, "Assumption failed: Should attempt to call service method 6 times");
            Assert.IsFalse(fired, "OnAfterInvoke was called when it should not have been!");
        }

        [Test]
        public void Proxy_OnAfterInvoke_InvokeInfo_SetCorrectly()
        {
            var service = Substitute.For<ITestService>();
            
            Request request = new Request { RequestMessage = "message" };

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.AreEqual("TestMethodComplexMulti", args.InvokeInfo.MethodName, "InvokeInfo.MethodName is not set correctly");
                    // parameters
                    Assert.AreEqual(2, args.InvokeInfo.Parameters.Length, "InvokeInfo.Parameters length is incorrect");
                    Assert.AreEqual("test", args.InvokeInfo.Parameters[0], "InvokeInfo.Parameters[0] is not set correctly");
                    Assert.AreEqual(request, args.InvokeInfo.Parameters[1], "InvokeInfo.Parameters[1] is not set correctly");

                    resetEvent.Set();
                };

                c.OnAfterInvoke += handler;
            });

            proxy.TestMethodComplexMulti("test", request);

            resetEvent.WaitOrFail("OnAfterInvoke not fired");
        }

        [Test]
        public void Proxy_OnAfterInvoke_ReturnValue_IsSetCorrectly()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod(Arg.Any<string>())
                .Returns("retval");

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.IsTrue(args.InvokeInfo.MethodHasReturnValue, "InvokeInfo.MethodHasReturnValue is not set correctly");
                    Assert.AreEqual("retval", args.InvokeInfo.ReturnValue, "InvokeInfo.ReturnValue is not set correctly");

                    resetEvent.Set();
                };
                c.OnAfterInvoke += handler;
            });

            proxy.TestMethod("test");

            resetEvent.WaitOrFail("OnAfterInvoke not fired");
        }

        [Test]
        public void Proxy_OnAfterInvoke_ReturnValue_ForValueTypeMethods_IsSetCorrectly()
        {
            var service = Substitute.For<ITestService>();

            service
                .IntMethod()
                .Returns(1337);

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.IsTrue(args.InvokeInfo.MethodHasReturnValue, "InvokeInfo.MethodHasReturnValue is not set correctly");
                    Assert.AreEqual(1337, args.InvokeInfo.ReturnValue, "InvokeInfo.ReturnValue is not set correctly");

                    resetEvent.Set();
                };

                c.OnAfterInvoke += handler;
            });

            proxy.IntMethod();

            resetEvent.WaitOrFail("OnAfterInvoke not fired");
        }

        [Test]
        public void Proxy_OnAfterInvoke_ReturnValue_ThrowsForVoidMethods()
        {
            var service = Substitute.For<ITestService>();

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.IsFalse(args.InvokeInfo.MethodHasReturnValue, "InvokeInfo.MethodHasReturnValue is not set correctly");
                    Assert.Throws<InvalidOperationException>(delegate { var x = args.InvokeInfo.ReturnValue; }, "InvokeInfo.ReturnValue did not throw!");

                    resetEvent.Set();
                };

                c.OnAfterInvoke += handler;
            });

            proxy.VoidMethod("test");

            resetEvent.WaitOrFail("OnAfterInvoke not fired");
        }

        #region AsyncProxy

        [Test]
        public async Task AsyncProxy_OnBeforeInvoke_IsFired()
        {
            var service = Substitute.For<ITestService>();

            bool fired = false;
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.OnBeforeInvoke += (sender, args) => fired = true;
            });
            
            await proxy.CallAsync(m => m.VoidMethod("test"));

            Assert.IsTrue(fired);
        }
        
        [Test]
        public async Task AsyncProxy_OnBeforeInvoke_ArgumentsSetCorrectly()
        {
            var service = Substitute.For<ITestService>();

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.AreEqual(false, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(0, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType is not set correctly");

                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });

            await proxy.CallAsync(m => m.VoidMethod("test"));

            resetEvent.WaitOrFail("OnBeforeInvoke hasn't been called");;
        }


        [Test]
        public async Task AsyncProxy_OnBeforeInvoke_IfRetry_FiredManyTimes()
        {
            var resetEvent = new AutoResetEvent(false);

            // set up a service method that throws first two times, and completes on the third time
            var service = Substitute.For<ITestService>();

            int mockFireCount = 0;
            service
                .When(m => m.VoidMethod(Arg.Any<string>()))
                .Do(_ =>
                {
                    // fail on first two calls, return on subsequent calls
                    mockFireCount++;
                    if (mockFireCount < 3)
                        throw new Exception();
                });

            int fireCount = 0;
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.MaximumRetries(10);
                c.RetryOnException<FaultException>();
                c.RetryOnException<FaultException<ExceptionDetail>>();

                OnInvokeHandler handler = (sender, args) =>
                {
                    fireCount++;
                    Assert.AreEqual(fireCount > 1, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(fireCount - 1, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType is not set correctly");

                    if (fireCount >= 2)
                        resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });
            
            await proxy.CallAsync(m => m.VoidMethod("test"));
            
            resetEvent.WaitOrFail("OnBeforeInvoke probably not called");

            Assert.AreEqual(3, fireCount, "Not called three times!");
        }

        [Test]
        public async Task AsyncProxy_OnBeforeInvoke_InvokeInfo_IsSet()
        {
            var resetEvent = new AutoResetEvent(false);

            var service = Substitute.For<ITestService>();
            
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.IsNotNull(args.InvokeInfo, "InvokeInfo is null when it should be set");
                    resetEvent.Set();
                };

                c.OnBeforeInvoke += handler;
            });
            
            await proxy.CallAsync(m => m.TestMethod("test"));

            resetEvent.WaitOrFail("OnBeforeInvoke not called");
        }

        [Test]
        public async Task AsyncProxy_OnAfterInvoke_IsFired()
        {
            var service = Substitute.For<ITestService>();           

            bool fired = false;
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.OnAfterInvoke += (sender, args) => fired = true;
            });

            await proxy.CallAsync(m => m.VoidMethod("test"));

            Assert.IsTrue(fired);
        }

        [Test]
        public async Task AsyncProxy_OnAfterInvoke_ArgumentsSetCorrectly()
        {
            var service = Substitute.For<ITestService>();
            
            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.AreEqual(false, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(0, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType is not set correctly");

                    resetEvent.Set();
                };
                c.OnAfterInvoke += handler;
            });

            await proxy.CallAsync(m => m.VoidMethod("test"));

            resetEvent.WaitOrFail("OnAfterInvoke hasn't been called");
        }

        [Test]
        public async Task AsyncProxy_OnAfterInvoke_InvokeInfo_IsSet()
        {
            var service = Substitute.For<ITestService>();
            
            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                OnInvokeHandler handler = (sender, args) =>
                {
                    Assert.IsNotNull(args.InvokeInfo, "InvokeInfo is null when it should be set");
                    resetEvent.Set();
                };
                c.OnAfterInvoke += handler;
            });
            
            await proxy.CallAsync(m => m.TestMethod("test"));

            resetEvent.WaitOrFail("OnAfterInvoke not called");
        }

        #endregion

        #endregion

        #region OnCallBegin and OnCallSuccess support

        [Test]
        public void Proxy_OnCallBegin_IsFired()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("test")
                .Returns("OK");

            var resetEvent = new AutoResetEvent(false);           
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnCallBegin += (invoker, args) =>
                {
                    Assert.That(args.InvokeInfo.MethodName, Is.EqualTo("TestMethod"));
                    
                    resetEvent.Set();
                };
            });

            proxy.TestMethod("test");

            resetEvent.WaitOrFail("OnCallBegin was not triggered");
        }

        [Test]
        public void Proxy_OnCallSuccess_IsFired()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("test")
                .Returns("OK")
                .AndDoes(_ => Thread.Sleep(500));

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnCallSuccess += (invoker, args) =>
                {
                    Assert.That(args.InvokeInfo.MethodName, Is.EqualTo("TestMethod"));
                    Assert.That(args.InvokeInfo.ReturnValue, Is.EqualTo("OK"));
                    Assert.That(args.CallDuration, Is.GreaterThan(TimeSpan.MinValue));

                    resetEvent.Set();
                };
            });

            proxy.TestMethod("test");

            resetEvent.WaitOrFail("OnCallSuccess was not triggered");
        }

        #region AsyncProxy

        [Test]
        public async Task AsyncProxy_OnCallBegin_IsFired()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("test")
                .Returns("OK");

            var resetEvent = new AutoResetEvent(false);           
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.OnCallBegin += (invoker, args) =>
                {
                    Assert.That(args.InvokeInfo.MethodName, Is.EqualTo("TestMethodAsync"));
                    
                    resetEvent.Set();
                };
            });

            await proxy.CallAsync(m => m.TestMethod("test"));

            resetEvent.WaitOrFail("OnCallBegin was not triggered");
        }

        [Test]
        public async Task AsyncProxy_OnCallSuccess_IsFired()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("test")
                .Returns("OK")
                .AndDoes(_ => Thread.Sleep(500));

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.OnCallSuccess += (invoker, args) =>
                {
                    Assert.That(args.InvokeInfo.MethodName, Is.EqualTo("TestMethodAsync"));
                    Assert.That(args.InvokeInfo.ReturnValue, Is.EqualTo("OK"));
                    Assert.That(args.CallDuration, Is.GreaterThan(TimeSpan.MinValue));

                    resetEvent.Set();
                };
            });

            await proxy.CallAsync(m => m.TestMethod("test"));

            resetEvent.WaitOrFail("OnCallSuccess was not triggered");
        }

        #endregion

        #endregion

        #region OnException support

        [Test]
        public void Proxy_OnException_NoException_NotFired()
        {
            var service = Substitute.For<ITestService>();
            
            bool hasFired = false;
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnException += (sender, args) => hasFired = true;
            });

            proxy.VoidMethod("test");

            Assert.IsFalse(hasFired);
        }

        [Test]
        public void Proxy_OnException_NoHandler_Compatibility()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            var proxy = service.StartHostAndProxy();
            Assert.Catch<FaultException>(() => proxy.VoidMethod("test"));
        }
        
        [Test]
        public void Proxy_OnException_IsFired()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            bool hasFired = false;
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnException += (sender, args) => hasFired = true;
            });

            Assert.Catch<FaultException>(() => proxy.VoidMethod("test"));
            Assert.IsTrue(hasFired);
        }

        [Test]
        public void Proxy_OnException_FiresOnEveryRetry()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            int fireCount = 0;
            var proxy = service.StartHostAndProxy(c =>
            {
                c.MaximumRetries(5);
                c.RetryOnException<FaultException>();
                c.SetDelayPolicy(() => { return new ConstantDelayPolicy(TimeSpan.FromSeconds(0)); });
                c.OnException += (sender, args) => fireCount++;
            });

            Assert.Catch<Exception>(() => proxy.VoidMethod("test"));
            Assert.AreEqual(6, fireCount);
        }

        [Test]
        public void Proxy_OnException_MultipleHandlersAreFired()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            bool hasFired1 = false;
            bool hasFired2 = false;
            bool hasFired3 = false;

            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnException += (sender, args) => hasFired1 = true;
                c.OnException += (sender, args) => hasFired2 = true;
                c.OnException += (sender, args) => hasFired3 = true;
            });

            Assert.Catch<FaultException>(() => proxy.VoidMethod("test"));

            Assert.IsTrue(hasFired1);
            Assert.IsTrue(hasFired2);
            Assert.IsTrue(hasFired3);
        }

        [Test]
        public void Proxy_OnException_InformationSetCorrectly()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                c.OnException += (sender, args) =>
                {
                    Assert.IsInstanceOf<FaultException>(args.Exception, "Exception");
                    Assert.AreEqual("VoidMethod", args.InvokeInfo.MethodName, "InvokeInfo.MethodName");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType");

                    resetEvent.Set();
                };
            });

            Assert.Catch<FaultException>(() => proxy.VoidMethod("test"));

            resetEvent.WaitOrFail("OnException not fired");
        }

        #region AsyncProxy

        [Test]
        public void AsyncProxy_OnException_NoHandler_Compatibility()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            var proxy = service.StartHostAndAsyncProxy();
            
            Assert.ThrowsAsync(
                typeof(FaultException),
                () => proxy.CallAsync(m => m.VoidMethod("test")));
        }

        [Test]
        public void AsyncProxy_OnException_IsFired()
        {
            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("test"))
                .Throw(new FaultException());

            bool hasFired = false;
            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.OnException += (sender, args) =>
                {
                    hasFired = true;
                    resetEvent.Set();
                };
            });

            Assert.ThrowsAsync<FaultException>(
                () => proxy.CallAsync(m => m.VoidMethod("test")));
            
            resetEvent.WaitOrFail("OnException not fired");

            Assert.IsTrue(hasFired);
        }

        #endregion

        #endregion

        #region ChannelFactory support

        [Test]
        public void Proxy_ChannelFactory_IfNotConfigured_UsesDefaultEndpoint()
        {
            WcfClientProxy.Create<ITestServiceSingleEndpointConfig>(c =>
            {
                // assert that the endpoint url is read from app.config
                Assert.AreEqual(new Uri("http://localhost:23456/TestService2"), c.ChannelFactory.Endpoint.Address.Uri);
            });
        }

        [Test]
        public void Proxy_ChannelFactory_UsesConfiguredEndpoint()
        {
            var service = Substitute.For<ITestService>();

            var proxy = service.StartHostAndProxy(c =>
            {
                c.SetEndpoint(new BasicHttpBinding(), new EndpointAddress("http://localhost:23456/SomeOtherTestServicUrl"));
                // assert that the endpoint is the same
                Assert.AreEqual(new Uri("http://localhost:23456/SomeOtherTestServicUrl"), c.ChannelFactory.Endpoint.Address.Uri);
            });
        }

        #endregion

        #region HandleRequestArgument

        [Test]
        public void HandleRequestArgument_ModifiesComplexRequest_BeforeSendingToService()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(m => new Response
                {
                    ResponseMessage = m.Arg<Request>().RequestMessage
                });

            var proxy = service.StartHostAndProxy(c =>
            {
                c.HandleRequestArgument<Request>(
                    where: (arg, param) => arg == null,
                    handler: arg =>
                    {
                        return new Request
                        {
                            RequestMessage = "default message"
                        };
                    });
            });

            proxy.TestMethodComplex(null);

            service
                .Received(1)
                .TestMethodComplex(Arg.Is<Request>(r => r.RequestMessage == "default message"));

            proxy.TestMethodComplex(new Request { RequestMessage = "set" });

            service
                .Received(1)
                .TestMethodComplex(Arg.Is<Request>(r => r.RequestMessage == "set"));
        }

        [Test]
        public void HandleRequestArgument_MatchesArgumentsOfSameType_BasedOnParameterName()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod(Arg.Any<string>(), Arg.Any<string>())
                .Returns("response");

            var proxy = service.StartHostAndProxy(c =>
            {               
                c.HandleRequestArgument<string>(
                    where: (arg, paramName) => paramName == "input",
                    handler: arg =>
                    {
                        return "always input";
                    });
                
                c.HandleRequestArgument<string>(
                    where: (arg, paramName) => paramName == "two",
                    handler: arg =>
                    {
                        return "always two";
                    });
            });

            proxy.TestMethod("first argument", "second argument");

            service
                .Received(1)
                .TestMethod("always input", "always two");
        }

        [Test]
        public void HandleRequestArgument_MatchesArgumentsByBaseTypes()
        {
            int handleRequestArgumentCounter = 0;

            var service = Substitute.For<ITestService>();

            service
                .TestMethodMixed(Arg.Any<string>(), Arg.Any<int>())
                .Returns(10);

            var proxy = service.StartHostAndProxy(c =>
            {
                c.HandleRequestArgument<object>(
                    handler: arg =>
                    {
                        handleRequestArgumentCounter++;
                    });
            });

            proxy.TestMethodMixed("first argument", 100);

            service
                .Received(1)
                .TestMethodMixed("first argument", 100);

            Assert.That(handleRequestArgumentCounter, Is.EqualTo(2));
        }

        #endregion

        #region HandleResponse

        [Test]
        public void HandleResponse_CanChangeResponse_ForSimpleResponseType()
        {
            var service = Substitute.For<ITestService>();

            const string expectedInput = "test";

            service
                .TestMethod(expectedInput)
                .Returns(m => m.Arg<string>());

            var proxy = service.StartHostAndProxy(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r => "hello: " + r);
            });

            var response = proxy.TestMethod(expectedInput);

            Assert.That(response, Is.EqualTo("hello: test"));
        }

        [Test]
        public void HandleResponse_ActionWithPredicate_CanInspectResponse_WithoutReturning()
        {
            var service = Substitute.For<ITestService>();

            const string expectedInput = "test";

            service
                .TestMethod(expectedInput)
                .Returns(m => m.Arg<string>());

            var resetEvent = new AutoResetEvent(false);
            var proxy = service.StartHostAndProxy(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r =>
                {
                    Assert.That(r, Is.EqualTo(expectedInput));
                    resetEvent.Set();
                });
            });

            var response = proxy.TestMethod(expectedInput);

            Assert.That(response, Is.EqualTo(expectedInput));

            resetEvent.WaitOrFail("Callback not fired");
        }

        [Test]
        public void HandleResponse_ActionWithoutPredicate_CanInspectResponse_WithoutReturning()
        {
            var resetEvent = new AutoResetEvent(false);

            var service = Substitute.For<ITestService>();

            const string expectedInput = "test";

            service
                .TestMethod(expectedInput)
                .Returns(m => m.Arg<string>());

            var proxy = service.StartHostAndProxy(c =>
            {
                c.HandleResponse<string>(handler: r =>
                {
                    Assert.That(r, Is.EqualTo(expectedInput));
                    resetEvent.Set();
                });
            });

            var response = proxy.TestMethod(expectedInput);

            Assert.That(response, Is.EqualTo(expectedInput));

            resetEvent.WaitOrFail("Callback not fired");
        }

        [Test]
        public void HandleResponse_CanChangeResponse_ForComplexResponseType()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(m => new Response
                {
                    ResponseMessage = m.Arg<Request>().RequestMessage
                });

            var proxy = service.StartHostAndProxy(c =>
            {
                c.HandleResponse<Response>(r =>
                {
                    r.ResponseMessage = "hello: " + r.ResponseMessage;
                    return r;
                });
            });

            var request = new Request { RequestMessage = "test" };
            var response = proxy.TestMethodComplex(request);

            Assert.That(response.ResponseMessage, Is.EqualTo("hello: test"));
        }    
    
        [Test]
        public void HandleResponse_CanChangeResponse_ForComplexResponse_InterfaceType()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(m => new Response
                {
                    ResponseMessage = m.Arg<Request>().RequestMessage,
                    StatusCode = 100
                });

            var proxy = service.StartHostAndProxy(c =>
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
            var response = proxy.TestMethodComplex(request);

            Assert.That(response.ResponseMessage, Is.EqualTo("error"));
            Assert.That(response.StatusCode, Is.EqualTo(1));
        }

        [Test]
        public void HandleResponse_CanThrowException()
        {
            var service = Substitute.For<ITestService>();

            const string expectedInput = "test";

            service
                .TestMethod(expectedInput)
                .Returns(m => m.Arg<string>());

            var proxy = service.StartHostAndProxy(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r =>
                {
                    throw new Exception(r);
                });
            });

            Assert.That(() => proxy.TestMethod(expectedInput), 
                Throws.TypeOf<Exception>()
                    .With.Message.EqualTo("test"));
        }

        [Test]
        public void HandleResponse_MultipleHandlersCanBeRunOnResponse()
        {
            var countdownEvent = new CountdownEvent(2);

            var service = Substitute.For<ITestService>();

            var serviceResponse = new Response()
            {
                ResponseMessage = "message",
                StatusCode = 100
            };

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(serviceResponse);

            var proxy = service.StartHostAndProxy(c =>
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

            var response = proxy.TestMethodComplex(new Request());
            
            if (!countdownEvent.Wait(250))
                Assert.Fail("Expected both callbacks to fire");
        }

        [Test]
        public void HandleResponse_MultipleHandlersCanBeRunOnResponse_WhereHandlersAreInheritingTypes()
        {
            var countdownEvent = new CountdownEvent(3);

            var service = Substitute.For<ITestService>();

            var serviceResponse = new Response()
            {
                ResponseMessage = "message",
                StatusCode = 100
            };

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(serviceResponse);

            var proxy = service.StartHostAndProxy(c =>
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

            var response = proxy.TestMethodComplex(new Request());
            
            if (!countdownEvent.Wait(250))
                Assert.Fail("Expected both callbacks to fire");
        }

        #region AsyncProxy


        [Test]
        public async Task Async_HandleResponse_CanChangeResponse_ForSimpleResponseType()
        {
            var service = Substitute.For<ITestService>();

            const string expectedInput = "test";

            service
                .TestMethod(expectedInput)
                .Returns(m => m.Arg<string>());

            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r => "hello: " + r);
            });

            var response = await proxy.CallAsync(m => m.TestMethod(expectedInput));

            Assert.That(response, Is.EqualTo("hello: test"));
        }
        
        [Test]
        public async Task Async_HandleResponse_CanChangeResponse_ForComplexResponseType()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(m => new Response
                {
                    ResponseMessage = m.Arg<Request>().RequestMessage
                });

            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.HandleResponse<Response>(r =>
                {
                    r.ResponseMessage = "hello: " + r.ResponseMessage;
                    return r;
                });
            });

            var request = new Request { RequestMessage = "test" };
            var response = await proxy.CallAsync(m => m.TestMethodComplex(request));

            Assert.That(response.ResponseMessage, Is.EqualTo("hello: test"));
        }    
    
        [Test]
        public async Task Async_HandleResponse_CanChangeResponse_ForComplexResponse_InterfaceType()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethodComplex(Arg.Any<Request>())
                .Returns(m => new Response
                {
                    ResponseMessage = m.Arg<Request>().RequestMessage,
                    StatusCode = 100
                });

            var proxy = service.StartHostAndAsyncProxy(c =>
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
            var response = await proxy.CallAsync(m => m.TestMethodComplex(request));

            Assert.That(response.ResponseMessage, Is.EqualTo("error"));
            Assert.That(response.StatusCode, Is.EqualTo(1));
        }

        [Test]
        public void Async_HandleResponse_CanThrowException()
        {
            var service = Substitute.For<ITestService>();

            const string expectedInput = "test";

            service
                .TestMethod(expectedInput)
                .Returns(m => m.Arg<string>());

            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r =>
                {
                    throw new Exception(r);
                });
            });

            var exception = Assert.ThrowsAsync<Exception>(
                () => proxy.CallAsync(m => m.TestMethod(expectedInput)));

            Assert.That(exception.Message, Is.EqualTo("test"));
        }


        [Test]
        public async Task Async_HandleResponse_ActionWithPredicate_CanInspectResponse_WithoutReturning()
        {
            var resetEvent = new AutoResetEvent(false);

            var service = Substitute.For<ITestService>();

            const string expectedInput = "test";

            service
                .TestMethod(expectedInput)
                .Returns(m => m.Arg<string>());

            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.HandleResponse<string>(where: r => r.StartsWith("te"), handler: r =>
                {
                    Assert.That(r, Is.EqualTo(expectedInput));
                    resetEvent.Set();
                });
            });

            var response = await proxy.CallAsync(m => m.TestMethod(expectedInput));

            Assert.That(response, Is.EqualTo(expectedInput));

            resetEvent.WaitOrFail("Callback not fired");
        }

        [Test]
        public async Task Async_HandleResponse_ActionWithoutPredicate_CanInspectResponse_WithoutReturning()
        {
            var resetEvent = new AutoResetEvent(false);

            var service = Substitute.For<ITestService>();

            const string expectedInput = "test";

            service
                .TestMethod(expectedInput)
                .Returns(m => m.Arg<string>());

            var proxy = service.StartHostAndAsyncProxy(c =>
            {
                c.HandleResponse<string>(handler: r =>
                {
                    Assert.That(r, Is.EqualTo(expectedInput));
                    resetEvent.Set();
                });
            });

            var response = await proxy.CallAsync(m => m.TestMethod(expectedInput));

            Assert.That(response, Is.EqualTo(expectedInput));

            resetEvent.WaitOrFail("Callback not fired");
        }

        #endregion

        #endregion

        #region Dynamic Async Invocation

        [Test]
        public async Task Async_DynamicConversion_Proxy_ReturnsExpectedValue_WhenCallingGeneratedAsyncMethod()
        {
            var service = Substitute.For<ITestService>();

            service
                .TestMethod("good")
                .Returns("OK");

            var proxy = service.StartHostAndProxy();
            
            // ITestService does not define TestMethodAsync, it's generated at runtime
            var result = await ((dynamic) proxy).TestMethodAsync("good");
            
            Assert.AreEqual("OK", result);
        }

        [Test]
        public async Task Async_DynamicConversion_Proxy_CanCallGeneratedAsyncVoidMethod()
        {
            var resetEvent = new AutoResetEvent(false);

            var service = Substitute.For<ITestService>();

            service
                .When(m => m.VoidMethod("good"))
                .Do(m =>
                {
                    Assert.That(m.Arg<string>(), Is.EqualTo("good"));
                    resetEvent.Set();
                });

            var proxy = service.StartHostAndProxy();
            
            // ITestService does not define VoidMethodAsync, it's generated at runtime
            await ((dynamic) proxy).VoidMethodAsync("good");

            if (!resetEvent.WaitOne(300))
                Assert.Fail("Timeout occurred when waiting for callback");
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

        [Test]
        public void Proxy_GivesProperException_IfInterfaceNotPublic()
        {
            Assert.Throws<InvalidOperationException>(delegate { WcfClientProxy.Create<IPrivateTestService>(); });
            // error message not checked here, but it should be quite readable
        }

        [Test]
        public void Proxy_GivesProperException_IfNotServiceContract()
        {
            Assert.Throws<InvalidOperationException>(delegate { WcfClientProxy.Create<INonServiceInterface>(); });
            // error message not checked here, but it should be quite readable
        }

        [Test]
        public void Proxy_GivesProperException_IfZeroOperationContracts()
        {
            Assert.Throws<InvalidOperationException>(delegate { WcfClientProxy.Create<INoOperationsInterface>(); });
            // error message not checked here, but it should be quite readable
        }

        #endregion
    }
}
