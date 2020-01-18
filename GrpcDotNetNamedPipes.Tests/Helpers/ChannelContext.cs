using System;
using GrpcDotNetNamedPipes.Tests.Generated;

namespace GrpcDotNetNamedPipes.Tests.Helpers
{
    public class ChannelContext : IDisposable
    {
        public Action OnDispose { get; set; }

        public TestService.TestServiceClient Client { get; set; }

        public TestServiceImpl Impl { get; set; }

        public void Dispose()
        {
            OnDispose.Invoke();
        }
    }
}