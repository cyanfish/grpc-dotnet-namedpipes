using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace GrpcDotNetNamedPipes.Internal
{
    public class PipeReader
    {
        private readonly PipeStream _pipeStream;
        private readonly TransportMessageHandler _messageHandler;
        private readonly Action _onDisconnected;
        private readonly NamedPipeTransport _transport;

        public PipeReader(PipeStream pipeStream, TransportMessageHandler messageHandler, Action onDisconnected)
        {
            _pipeStream = pipeStream;
            _messageHandler = messageHandler;
            _onDisconnected = onDisconnected;
            _transport = new NamedPipeTransport(_pipeStream);
        }
        
        public async Task ReadLoop()
        {
            try
            {
                while (_pipeStream.IsConnected)
                {
                    await _transport.Read(_messageHandler).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // TODO: Log if unexpected
            }
            finally
            {
                _onDisconnected?.Invoke();
            }
        }
    }
}