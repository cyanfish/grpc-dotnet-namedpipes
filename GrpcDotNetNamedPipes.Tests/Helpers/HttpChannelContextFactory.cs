using System.Linq;
using Grpc.Core;
using GrpcDotNetNamedPipes.Tests.Generated;

namespace GrpcDotNetNamedPipes.Tests.Helpers
{
    public class HttpChannelContextFactory : ChannelContextFactory
    {
        private static readonly ChannelOption[] Options = {
            new ChannelOption(ChannelOptions.MaxReceiveMessageLength, 1024 * 1024 * 1024),
            new ChannelOption(ChannelOptions.MaxSendMessageLength, 1024 * 1024 * 1024)
        };
        private int _port;

        public override ChannelContext Create()
        {
            var impl = new TestServiceImpl();
            var server = new Server(Options)
            {
                Services = {TestService.BindService(impl)},
                Ports = {new ServerPort("localhost", 0, ServerCredentials.Insecure)}
            };
            server.Start();
            _port = server.Ports.First().BoundPort;
            return new ChannelContext
            {
                Impl = impl,
                Client = CreateClient(),
                OnDispose = () => server.KillAsync()
            };
        }

        public override TestService.TestServiceClient CreateClient()
        {
            var channel = new Channel(
                "localhost",
                _port,
                ChannelCredentials.Insecure,
                Options);
            return new TestService.TestServiceClient(channel);
        }

        public override string ToString()
        {
            return "http";
        }
    }
}