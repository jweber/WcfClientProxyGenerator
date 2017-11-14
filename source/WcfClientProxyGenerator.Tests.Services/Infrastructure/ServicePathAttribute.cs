using System;

namespace WcfClientProxyGenerator.Tests.Services.Infrastructure
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class ServicePathAttribute : Attribute
    {
        public ServicePathAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}