using GrpcDotNetNamedPipes.Tests.Generated;

namespace GrpcDotNetNamedPipes.Tests.Helpers
{
    public abstract class ChannelContextFactory
    {
        public abstract ChannelContext Create();
        public abstract TestService.TestServiceClient CreateClient();
    }
}