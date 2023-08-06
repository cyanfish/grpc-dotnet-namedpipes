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

using Google.Protobuf;
using Google.Protobuf.Collections;

namespace GrpcDotNetNamedPipes.Internal.Protocol;

internal class NamedPipeTransport
{
    private const int MessageBufferSize = 16 * 1024; // 16 kiB

    private readonly byte[] _messageBuffer = new byte[MessageBufferSize];
    private readonly PipeStream _pipeStream;
    private readonly ConnectionLogger _logger;
    private readonly WriteTransactionQueue _txQueue;

    public NamedPipeTransport(PipeStream pipeStream, ConnectionLogger logger)
    {
        _pipeStream = pipeStream;
        _logger = logger;
        _txQueue = new WriteTransactionQueue(pipeStream);
    }

    private async Task<MemoryStream> ReadPacketFromPipe()
    {
        var packet = new MemoryStream();
        if (PlatformConfig.SizePrefix)
        {
            await ReadPacketWithSizePrefix(packet);
        }
        else
        {
            await ReadPacketWithMessage(packet);
        }

        packet.Position = 0;
        return packet;
    }

    private async Task ReadPacketWithMessage(MemoryStream packet)
    {
        do
        {
            int readBytes = await _pipeStream.ReadAsync(_messageBuffer, 0, MessageBufferSize).ConfigureAwait(false);
            packet.Write(_messageBuffer, 0, readBytes);
        } while (!_pipeStream.IsMessageComplete);
    }

    private async Task ReadPacketWithSizePrefix(MemoryStream packet)
    {
        int bytesToRead = await ReadSizePrefix();
        do
        {
            var bytesToReadIntoBuffer = Math.Min(bytesToRead, MessageBufferSize);
            int readBytes = await _pipeStream.ReadAsync(_messageBuffer, 0, bytesToReadIntoBuffer)
                .ConfigureAwait(false);
            if (readBytes == 0)
            {
                throw new EndOfPipeException();
            }
            packet.Write(_messageBuffer, 0, readBytes);
            bytesToRead -= readBytes;
        } while (bytesToRead > 0);
    }

    private async Task<int> ReadSizePrefix()
    {
        int readBytes = await _pipeStream.ReadAsync(_messageBuffer, 0, 4).ConfigureAwait(false);
        if (readBytes == 0)
        {
            throw new EndOfPipeException();
        }
        if (readBytes != 4)
        {
            throw new InvalidOperationException("Unexpected size prefix");
        }
        return BitConverter.ToInt32(_messageBuffer, 0);
    }

    public async Task<bool> Read(TransportMessageHandler messageHandler)
    {
        var packet = await ReadPacketFromPipe().ConfigureAwait(false);
        while (packet.Position < packet.Length)
        {
            var message = new TransportMessage();
            message.MergeDelimitedFrom(packet);
            switch (message.DataCase)
            {
                case TransportMessage.DataOneofCase.RequestInit:
                    _logger.ConnectionId = message.RequestInit.ConnectionId;
                    _logger.Log($"Received <RequestInit> for '{message.RequestInit.MethodFullName}'");
                    messageHandler.HandleRequestInit(message.RequestInit.MethodFullName,
                        message.RequestInit.Deadline?.ToDateTime());
                    break;
                case TransportMessage.DataOneofCase.Headers:
                    _logger.Log("Received <Headers>");
                    var headerMetadata = ConstructMetadata(message.Headers.Metadata);
                    messageHandler.HandleHeaders(headerMetadata);
                    break;
                case TransportMessage.DataOneofCase.PayloadInfo:
                    _logger.Log($"Received <PayloadInfo> with {message.PayloadInfo.Size} bytes");
                    var payload = new byte[message.PayloadInfo.Size];
                    if (message.PayloadInfo.InSamePacket)
                    {
                        packet.Read(payload, 0, payload.Length);
                    }
                    else
                    {
                        _pipeStream.Read(payload, 0, payload.Length);
                    }

                    messageHandler.HandlePayload(payload);
                    break;
                case TransportMessage.DataOneofCase.RequestControl:
                    switch (message.RequestControl)
                    {
                        case RequestControl.Cancel:
                            _logger.Log("Received <Cancel>");
                            messageHandler.HandleCancel();
                            break;
                        case RequestControl.StreamEnd:
                            _logger.Log("Received <StreamEnd>");
                            messageHandler.HandleStreamEnd();
                            break;
                    }

                    break;
                case TransportMessage.DataOneofCase.Trailers:
                    _logger.Log($"Received <Trailers> with status '{message.Trailers.StatusCode}'");
                    var trailerMetadata = ConstructMetadata(message.Trailers.Metadata);
                    var status = new Status((StatusCode) message.Trailers.StatusCode,
                        message.Trailers.StatusDetail);
                    messageHandler.HandleTrailers(trailerMetadata, status);
                    // Stop reading after receiving trailers
                    return false;
            }
        }
        return true;
    }

    private static Metadata ConstructMetadata(RepeatedField<MetadataEntry> entries)
    {
        var metadata = new Metadata();
        foreach (var entry in entries)
        {
            switch (entry.ValueCase)
            {
                case MetadataEntry.ValueOneofCase.ValueString:
                    metadata.Add(new Metadata.Entry(entry.Name, entry.ValueString));
                    break;
                case MetadataEntry.ValueOneofCase.ValueBytes:
                    metadata.Add(new Metadata.Entry(entry.Name, entry.ValueBytes.ToByteArray()));
                    break;
            }
        }

        return metadata;
    }

    public WriteTransaction Write()
    {
        return new WriteTransaction(_txQueue, _logger);
    }
}