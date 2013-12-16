using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using WcfClientProxyGenerator.Policy;
using WcfClientProxyGenerator.Tests.Infrastructure;
using WcfClientProxyGenerator.Util;

namespace WcfClientProxyGenerator.Tests
{
    [TestFixture]
    public class ProxyTests
    {
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
        public void Proxy_ReturnsExpectedValue_WhenCallingService()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));
            
            var result = proxy.TestMethod("good");
            Assert.AreEqual("OK", result);
        }

        [Test]
        public void Proxy_CanCallVoidMethod()
        {
            var mockService = new Mock<ITestService>();
            mockService
                .Setup(m => m.VoidMethod("good"))
                .Callback<string>(input =>
                {
                    Assert.That(input, Is.EqualTo("good"));
                });

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));
            proxy.VoidMethod("good");
        }

        [Test]
        public void MultipleProxies_ReturnExpectedValues_WhenCallingServices()
        {
            var mockService1 = new Mock<ITestService>();
            mockService1.Setup(m => m.TestMethod("service1")).Returns("OK from service 1");

            var mockService2 = new Mock<ITestService2>();
            mockService2.Setup(m => m.TestMethod("service2")).Returns("OK from service 2");

            var serviceHost1 = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService1));
            var serviceHost2 = InProcTestFactory.CreateHost<ITestService2>(new TestService2Impl(mockService2));

            var proxy1 = WcfClientProxy.Create<ITestService>(
                    c => c.SetEndpoint(serviceHost1.Binding, serviceHost1.EndpointAddress));

            var proxy2 = WcfClientProxy.Create<ITestService2>(
                    c => c.SetEndpoint(serviceHost2.Binding, serviceHost2.EndpointAddress));

            Assert.AreEqual("OK from service 1", proxy1.TestMethod("service1"));
            Assert.AreEqual("OK from service 2", proxy2.TestMethod("service2"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingSimpleMethod()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod("good")).Returns("OK");
            mockService.Setup(m => m.TestMethod("bad")).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethod("bad"), Throws.Exception);
            Assert.That(() => proxy.TestMethod("good"), Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingComplexTypeMethod()
        {
            var badRequest = new Request() { RequestMessage = "bad" };
            var goodRequest = new Request() { RequestMessage = "good" };

            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethodComplex(goodRequest)).Returns(new Response() { ResponseMessage = "OK" });
            mockService.Setup(m => m.TestMethodComplex(badRequest)).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethodComplex(badRequest), Throws.Exception);
            Assert.That(() => proxy.TestMethodComplex(goodRequest).ResponseMessage, Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_RecoversFromFaultedState_WhenCallingMultipleParameterComplexTypeMethod()
        {
            var badRequest = new Request() { RequestMessage = "bad" };
            var goodRequest = new Request() { RequestMessage = "good" };

            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethodComplexMulti("good", goodRequest)).Returns(new Response() { ResponseMessage = "OK" });
            mockService.Setup(m => m.TestMethodComplexMulti("bad", badRequest)).Throws<Exception>();

            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            // Will fault the channel
            Assert.That(() => proxy.TestMethodComplexMulti("bad", badRequest), Throws.Exception);
            Assert.That(() => proxy.TestMethodComplexMulti("good", goodRequest).ResponseMessage, Is.EqualTo("OK"));
        }

        [Test]
        public void Proxy_CanBeGeneratedForInheritingServiceInterface()
        {
            var mockTestService = new Mock<ITestService>();
            var mockChildService = new Mock<IChildService>();

            mockChildService
                .Setup(m => m.ChildMethod(It.IsAny<string>()))
                .Returns("OK");

            var serviceHost = InProcTestFactory.CreateHost<IChildService>(new ChildServiceImpl(mockTestService, mockChildService));

            var proxy = WcfClientProxy.Create<IChildService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            Assert.That(() => proxy.ChildMethod("test"), Is.EqualTo("OK"));
        }

        #region Out Parameter Support

        [Test]
        public void Proxy_CanBeGeneratedForOperationWithSingleOutParameter()
        {
            var mockTestService = new Mock<IOutParamTestService>();

            byte[] expectedOutParam = { 0x00, 0x01 };
            mockTestService
                .Setup(m => m.SingleOutParam(out expectedOutParam))
                .Returns(1);

            var serviceHost = InProcTestFactory.CreateHost<IOutParamTestService>(new OutParamTestServiceImpl(mockTestService));

            var proxy = WcfClientProxy.Create<IOutParamTestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            byte[] outParam;
            int result = proxy.SingleOutParam(out outParam);

            Assert.That(result, Is.EqualTo(1));
            Assert.That(outParam, Is.EqualTo(expectedOutParam));
        }

        [Test]
        public void Proxy_CanBeGeneratedForOperationWithMultipleOutParameters()
        {
            var mockTestService = new Mock<IOutParamTestService>();

            byte[] expectedOut1Value = { 0x00, 0x01 };
            string expectedOut2Value = "message";
            mockTestService
                .Setup(m => m.MultipleOutParams(out expectedOut1Value, out expectedOut2Value))
                .Returns(1);

            var serviceHost = InProcTestFactory.CreateHost<IOutParamTestService>(new OutParamTestServiceImpl(mockTestService));

            var proxy = WcfClientProxy.Create<IOutParamTestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

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
            var mockTestService = new Mock<IOutParamTestService>();

            int expectedOut1Value = 25;
            mockTestService
                .Setup(m => m.MixedParams(1, out expectedOut1Value, "test"))
                .Returns(1);

            var serviceHost = InProcTestFactory.CreateHost<IOutParamTestService>(new OutParamTestServiceImpl(mockTestService));

            var proxy = WcfClientProxy.Create<IOutParamTestService>(c => c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress));

            int out1Value;
            int result = proxy.MixedParams(1, out out1Value, "test");

            Assert.That(result, Is.EqualTo(1));
            Assert.That(out1Value, Is.EqualTo(expectedOut1Value));
        }

        #endregion

        #region OnBeforeInvoke and OnAfterInvoke support

        [Test]
        public void Proxy_OnBeforeInvoke_IsFired()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            bool fired = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.OnBeforeInvoke += (sender, args) => fired = true;
            });
            proxy.VoidMethod("test");
            Assert.IsTrue(fired);
        }

        [Test]
        public void Proxy_OnBeforeInvoke_Multiple_AreFired()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            bool fired1 = false;
            bool fired2 = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
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
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            bool fired = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) => fired = true;
                c.OnBeforeInvoke += handler;
                c.OnBeforeInvoke -= handler;
            });
            proxy.VoidMethod("test");
            Assert.IsFalse(fired);
        }

        [Test]
        public void Proxy_OnBeforeInvoke_ArgumentsSetCorrectly()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.AreEqual(false, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(0, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType is not set correctly");
                };
                c.OnBeforeInvoke += handler;
            });
            proxy.VoidMethod("test");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_IfRetry_FiredManyTimes()
        {
            // set up a service method that throws first two times, and completes on the third time
            var mockService = new Mock<ITestService>();
            int mockFireCount = 0;
            mockService.Setup(m => m.VoidMethod(It.IsAny<string>()))
                .Callback(() =>
                {
                    // fail on first two calls, return on subsequent calls
                    mockFireCount++;
                    if (mockFireCount < 3)
                        throw new Exception();
                });
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            int fireCount = 0;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.MaximumRetries(10);
                c.RetryOnException<FaultException>();

                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    fireCount++;
                    Assert.AreEqual(fireCount > 1, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(fireCount - 1, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType is not set correctly");
                };
                c.OnBeforeInvoke += handler;
            });
            proxy.VoidMethod("test");
            Assert.AreEqual(3, fireCount, "Not called three times!");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_InvokeInfo_IsSet()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.IsNotNull(args.InvokeInfo, "InvokeInfo is null when it should be set");
                };
                c.OnBeforeInvoke += handler;
            });
            proxy.TestMethod("test");
        }

        [Test]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            Request request = new Request() { RequestMessage = "message" };
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.AreEqual("TestMethodComplexMulti", args.InvokeInfo.MethodName, "InvokeInfo.MethodName is not set correctly");
                    // parameters
                    Assert.AreEqual(2, args.InvokeInfo.Parameters.Length, "InvokeInfo.Parameters length is incorrect");
                    Assert.AreEqual("test", args.InvokeInfo.Parameters[0], "InvokeInfo.Parameters[0] is not set correctly");
                    Assert.AreEqual(request, args.InvokeInfo.Parameters[1], "InvokeInfo.Parameters[1] is not set correctly");
                };
                c.OnBeforeInvoke += handler;
            });
            proxy.TestMethodComplexMulti("test", request);
        }

        [Test]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly_NoParameters()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.AreEqual("VoidMethodNoParameters", args.InvokeInfo.MethodName, "InvokeInfo.MethodName is not set correctly");
                    Assert.AreEqual(0, args.InvokeInfo.Parameters.Length, "InvokeInfo.Parameters length is incorrect");
                };
                c.OnBeforeInvoke += handler;
            });
            proxy.VoidMethodNoParameters();
        }

        [Test]
        public void Proxy_OnBeforeInvoke_InvokeInfo_SetCorrectly_IntParameter()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.AreEqual("VoidMethodIntParameter", args.InvokeInfo.MethodName, "InvokeInfo.MethodName is not set correctly");
                    Assert.AreEqual(1, args.InvokeInfo.Parameters.Length, "InvokeInfo.Parameters length is incorrect");
                    Assert.AreEqual(1337, args.InvokeInfo.Parameters[0], "InvokeInfo.Parameters[0] is not set correctly");
                };
                c.OnBeforeInvoke += handler;
            });
            proxy.VoidMethodIntParameter(1337);
        }

        [Test]
        public void Proxy_OnBeforeInvoke_ReturnValue_Throws()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.IsFalse(args.InvokeInfo.MethodHasReturnValue, "InvokeInfo.MethodHasReturnValue is not set correctly");
                    Assert.Throws<InvalidOperationException>(delegate { var x = args.InvokeInfo.ReturnValue; }, "InvokeInfo.ReturnValue did not throw!");
                };
                c.OnBeforeInvoke += handler;
            });
            proxy.TestMethod("test");
        }

        [Test]
        public void Proxy_OnAfterInvoke_IsFired()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            bool fired = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.OnAfterInvoke += (sender, args) => fired = true;
            });
            proxy.VoidMethod("test");
            Assert.IsTrue(fired);
        }

        [Test]
        public void Proxy_OnAfterInvoke_Multiple_AreFired()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            bool fired1 = false;
            bool fired2 = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
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
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            bool fired = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) => fired = true;
                c.OnAfterInvoke += handler;
                c.OnAfterInvoke -= handler;
            });
            proxy.VoidMethod("test");
            Assert.IsFalse(fired);
        }

        [Test]
        public void Proxy_OnAfterInvoke_ArgumentsSetCorrectly()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.AreEqual(false, args.IsRetry, "IsRetry is not set correctly");
                    Assert.AreEqual(0, args.RetryCounter, "RetryCounter is not set correctly");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType is not set correctly");
                };
                c.OnAfterInvoke += handler;
            });
            proxy.VoidMethod("test");
        }

        [Test]
        public void Proxy_OnAfterInvoke_IfException_IsNotFired()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.VoidMethod(It.IsAny<string>())).Throws<Exception>();
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            bool fired = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.OnAfterInvoke += (sender, args) => fired = true;
            });
            try { proxy.VoidMethod("test"); } catch { }
            Assert.IsFalse(fired, "OnAfterInvoke was called when it should not have been!");
        }

        [Test]
        public void Proxy_OnAfterInvoke_IfExceptionAndIfRetryCountUsedUp_IsNotFired()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.VoidMethod(It.IsAny<string>())).Throws<Exception>();
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            int attempts = 0; // number of times method has been attempted to be called
            bool fired = false; // true if OnAfterInvoke event was fired
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.MaximumRetries(5);
                c.RetryOnException<FaultException>();
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.OnBeforeInvoke += (sender, args) => attempts++;
                c.OnAfterInvoke += (sender, args) => fired = true;
            });
            try { proxy.VoidMethod("test"); }
            catch { }
            Assert.AreEqual(5, attempts, "Assumption failed: Should attempt to call service method 5 times");
            Assert.IsFalse(fired, "OnAfterInvoke was called when it should not have been!");
        }

        [Test]
        public void Proxy_OnAfterInvoke_InvokeInfo_SetCorrectly()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            Request request = new Request() { RequestMessage = "message" };
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.AreEqual("TestMethodComplexMulti", args.InvokeInfo.MethodName, "InvokeInfo.MethodName is not set correctly");
                    // parameters
                    Assert.AreEqual(2, args.InvokeInfo.Parameters.Length, "InvokeInfo.Parameters length is incorrect");
                    Assert.AreEqual("test", args.InvokeInfo.Parameters[0], "InvokeInfo.Parameters[0] is not set correctly");
                    Assert.AreEqual(request, args.InvokeInfo.Parameters[1], "InvokeInfo.Parameters[1] is not set correctly");
                };
                c.OnAfterInvoke += handler;
            });
            proxy.TestMethodComplexMulti("test", request);
        }

        [Test]
        public void Proxy_OnAfterInvoke_ReturnValue_IsSetCorrectly()
        {
            Mock<ITestService> mockService = new Mock<ITestService>();
            mockService.Setup(m => m.TestMethod(It.IsAny<string>())).Returns("retval");
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.IsTrue(args.InvokeInfo.MethodHasReturnValue, "InvokeInfo.MethodHasReturnValue is not set correctly");
                    Assert.AreEqual("retval", args.InvokeInfo.ReturnValue, "InvokeInfo.ReturnValue is not set correctly");
                };
                c.OnAfterInvoke += handler;
            });
            proxy.TestMethod("test");
        }

        [Test]
        public void Proxy_OnAfterInvoke_ReturnValue_ForValueTypeMethods_IsSetCorrectly()
        {
            Mock<ITestService> mockService = new Mock<ITestService>();
            mockService.Setup(m => m.IntMethod()).Returns(1337);
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.IsTrue(args.InvokeInfo.MethodHasReturnValue, "InvokeInfo.MethodHasReturnValue is not set correctly");
                    Assert.AreEqual(1337, args.InvokeInfo.ReturnValue, "InvokeInfo.ReturnValue is not set correctly");
                };
                c.OnAfterInvoke += handler;
            });
            proxy.IntMethod();
        }

        [Test]
        public void Proxy_OnAfterInvoke_ReturnValue_ThrowsForVoidMethods()
        {
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl());

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                OnInvokeHandler handler = (object sender, OnInvokeHandlerArguments args) =>
                {
                    Assert.IsFalse(args.InvokeInfo.MethodHasReturnValue, "InvokeInfo.MethodHasReturnValue is not set correctly");
                    Assert.Throws<InvalidOperationException>(delegate { var x = args.InvokeInfo.ReturnValue; }, "InvokeInfo.ReturnValue did not throw!");
                };
                c.OnAfterInvoke += handler;
            });
            proxy.VoidMethod("test");
        }

        #endregion

        #region OnException support
        [Test]
        public void Proxy_OnException_NoException_NotFired()
        {
            var mockService = new Mock<ITestService>();
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            bool hasFired = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.OnException += (sender, args) => hasFired = true;
            });
            proxy.VoidMethod("test");
            Assert.IsFalse(hasFired);
        }

        [Test]
        public void Proxy_OnException_NoHandler_Compatibility()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.VoidMethod("test")).Throws(new FaultException());
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
            });
            Assert.Catch<FaultException>(() => proxy.VoidMethod("test"));
        }

        [Test]
        public void Proxy_OnException_IsFired()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.VoidMethod("test")).Throws(new FaultException());
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            bool hasFired = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.OnException += (sender, args) => hasFired = true;
            });
            Assert.Catch<FaultException>(() => proxy.VoidMethod("test"));
            Assert.IsTrue(hasFired);
        }

        [Test]
        public void Proxy_OnException_FiresOnEveryRetry()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.VoidMethod("test")).Throws(new FaultException());
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            int fireCount = 0;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.MaximumRetries(5);
                c.RetryOnException<FaultException>();
                c.SetDelayPolicy(() => { return new ConstantDelayPolicy(TimeSpan.FromSeconds(0)); });
                c.OnException += (sender, args) => fireCount++;
            });
            Assert.Catch<Exception>(() => proxy.VoidMethod("test"));
            Assert.AreEqual(5, fireCount);
        }

        [Test]
        public void Proxy_OnException_MultipleHandlersAreFired()
        {
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.VoidMethod("test")).Throws(new FaultException());
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            bool hasFired1 = false;
            bool hasFired2 = false;
            bool hasFired3 = false;
            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
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
            var mockService = new Mock<ITestService>();
            mockService.Setup(m => m.VoidMethod("test")).Throws(new FaultException());
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(serviceHost.Binding, serviceHost.EndpointAddress);
                c.OnException += (sender, args) =>
                {
                    Assert.IsInstanceOf<FaultException>(args.Exception, "Exception");
                    Assert.AreEqual("VoidMethod", args.InvokeInfo.MethodName, "InvokeInfo.MethodName");
                    Assert.AreEqual(typeof(ITestService), args.ServiceType, "ServiceType");
                };
            });
            Assert.Catch<FaultException>(() => proxy.VoidMethod("test"));
        }

        #endregion

        #region ChannelFactory support
        [Test]
        public void Proxy_ChannelFactory_IfNotConfigured_UsesDefaultEndpoint()
        {
            var mockService = new Mock<ITestServiceSingleEndpointConfig>();
            var serviceHost = InProcTestFactory.CreateHost<ITestServiceSingleEndpointConfig>(new TestServiceSingleEndpointConfigImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestServiceSingleEndpointConfig>(c =>
            {
                // assert that the endpoint url is read from app.config
                Assert.AreEqual(new Uri("http://localhost:23456/TestService2"), c.ChannelFactory.Endpoint.Address.Uri);
            });
        }

        [Test]
        public void Proxy_ChannelFactory_UsesConfiguredEndpoint()
        {
            var mockService = new Mock<ITestService>();
            var serviceHost = InProcTestFactory.CreateHost<ITestService>(new TestServiceImpl(mockService));

            var proxy = WcfClientProxy.Create<ITestService>(c =>
            {
                c.SetEndpoint(new BasicHttpBinding(), new EndpointAddress("http://localhost:23456/SomeOtherTestServicUrl"));
                // assert that the endpoint is the same
                Assert.AreEqual(new Uri("http://localhost:23456/SomeOtherTestServicUrl"), c.ChannelFactory.Endpoint.Address.Uri);
            });
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
            var mockService = new Mock<IPrivateTestService>();
            Assert.Throws<InvalidOperationException>(delegate { WcfClientProxy.Create<IPrivateTestService>(); });
            // error message not checked here, but it should be quite readable
        }

        [Test]
        public void Proxy_GivesProperException_IfNotServiceContract()
        {
            var mockService = new Mock<INonServiceInterface>();
            Assert.Throws<InvalidOperationException>(delegate { WcfClientProxy.Create<INonServiceInterface>(); });
            // error message not checked here, but it should be quite readable
        }

        [Test]
        public void Proxy_GivesProperException_IfZeroOperationContracts()
        {
            var mockService = new Mock<INoOperationsInterface>();
            Assert.Throws<InvalidOperationException>(delegate { WcfClientProxy.Create<INoOperationsInterface>(); });
            // error message not checked here, but it should be quite readable
        }

        #endregion
    }
}
