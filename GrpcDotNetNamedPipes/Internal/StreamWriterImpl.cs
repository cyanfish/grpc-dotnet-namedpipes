using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace GrpcDotNetNamedPipes.Internal
{
    internal class StreamWriterImpl<T> : IClientStreamWriter<T>, IServerStreamWriter<T>
    {
        private readonly NamedPipeTransport _stream;
        private readonly CancellationToken _cancellationToken;
        private readonly Marshaller<T> _marshaller;

        public StreamWriterImpl(NamedPipeTransport stream, CancellationToken cancellationToken, Marshaller<T> marshaller)
        {
            _stream = stream;
            _cancellationToken = cancellationToken;
            _marshaller = marshaller;
        }

        public WriteOptions WriteOptions { get; set; }

        public Task WriteAsync(T message)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(_cancellationToken);
            }

            var payload = _marshaller.Serializer(message);
            _stream.Write().Payload(payload).Commit();
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            _stream.Write().RequestStreamEnd().Commit();
            return Task.CompletedTask;
        }
    }
}