using System;
using GrpcDotNetNamedPipes.Tests.Generated;

namespace GrpcDotNetNamedPipes.Tests.Helpers
{
    public class NamedPipeChannelContextFactory : ChannelContextFactory
    {
        private readonly string _pipeName = $"GrpcNamedPipeTests/{Guid.NewGuid()}";
            
        public override ChannelContext Create()
        {
            var impl = new TestServiceImpl();
            var server = new NamedPipeServer(_pipeName);
            TestService.BindService(server.ServiceBinder, impl);
            server.Start();
            return new ChannelContext
            {
                Impl = impl,
                Client = CreateClient(),
                OnDispose = () => server.Kill()
            };
        }

        public override TestService.TestServiceClient CreateClient()
        {
            var channel = new NamedPipeChannel(".", _pipeName);
            return new TestService.TestServiceClient(channel);
        }

        public override string ToString()
        {
            return "pipe";
        }
    }
}