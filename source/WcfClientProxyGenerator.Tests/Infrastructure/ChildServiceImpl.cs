using Moq;

namespace WcfClientProxyGenerator.Tests.Infrastructure
{
    public class ChildServiceImpl : TestServiceImpl, IChildService
    {
        private readonly Mock<ITestService> _mockTestService;
        private readonly Mock<IChildService> _mockChildService;

        public ChildServiceImpl()
        {}

        public ChildServiceImpl(
            Mock<ITestService> mockTestService,
            Mock<IChildService> mockChildService)
            : base(mockTestService)
        {
            _mockTestService = mockTestService;
            _mockChildService = mockChildService;
        }


        public string ChildMethod(string input)
        {
            if (_mockChildService != null)
                return _mockChildService.Object.ChildMethod(input);

            return string.Format("ChildMethod: {0}", input);
        }
    }
}
