/*
 * Copyright 2020 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * https://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace GrpcDotNetNamedPipes.Internal;

internal class PipeReader
{
    private readonly PipeStream _pipeStream;
    private readonly TransportMessageHandler _messageHandler;
    private readonly Action _onDisconnected;
    private readonly Action<Exception> _onError;
    private readonly NamedPipeTransport _transport;

    public PipeReader(PipeStream pipeStream, TransportMessageHandler messageHandler, Action onDisconnected,
        Action<Exception> onError = null)
    {
        _pipeStream = pipeStream;
        _messageHandler = messageHandler;
        _onDisconnected = onDisconnected;
        _onError = onError;
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
        catch (EndOfPipeException)
        {
        }
        catch (Exception error)
        {
            _onError?.Invoke(error);
        }
        finally
        {
            _onDisconnected?.Invoke();
        }
    }
}