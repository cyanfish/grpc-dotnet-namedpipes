using System;
using Grpc.Core;

namespace GrpcDotNetNamedPipes.Internal
{
    public abstract class TransportMessageHandler
    {
        public virtual void HandleRequestInit(string methodFullName, DateTime? deadline) => throw new InvalidOperationException();
        public virtual void HandleHeaders(Metadata headers) => throw new InvalidOperationException();
        public virtual void HandlePayload(byte[] payload) => throw new InvalidOperationException();
        public virtual void HandleCancel() => throw new InvalidOperationException();
        public virtual void HandleStreamEnd() => throw new InvalidOperationException();
        public virtual void HandleTrailers(Metadata trailers, Status status) => throw new InvalidOperationException();
    }
}