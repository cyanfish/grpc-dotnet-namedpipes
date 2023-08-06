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
using Google.Protobuf.WellKnownTypes;

namespace GrpcDotNetNamedPipes.Internal.Protocol;

internal class WriteTransaction
{
    private const int PayloadInSeparatePacketThreshold = 15 * 1024; // 15 kiB

    private readonly WriteTransactionQueue _txQueue;
    private readonly ConnectionLogger _logger;
    private readonly MemoryStream _packetBuffer = new();
    private readonly List<byte[]> _trailingPayloads = new();

    public WriteTransaction(WriteTransactionQueue txQueue, ConnectionLogger logger)
    {
        _txQueue = txQueue;
        _logger = logger;
    }

    public void Commit()
    {
        _txQueue.Add(this);
    }

    public void WriteTo(PipeStream pipeStream)
    {
        lock (pipeStream)
        {
            if (_packetBuffer.Length > 0)
            {
                if (PlatformConfig.SizePrefix)
                {
                    pipeStream.Write(BitConverter.GetBytes(_packetBuffer.Length), 0, 4);
                }
                _packetBuffer.WriteTo(pipeStream);
            }

            foreach (var payload in _trailingPayloads)
            {
                pipeStream.Write(payload, 0, payload.Length);
            }
        }
    }

    public void MergeFrom(WriteTransaction other)
    {
        other._packetBuffer.WriteTo(_packetBuffer);
        _trailingPayloads.AddRange(other._trailingPayloads);
    }

    private WriteTransaction AddMessage(TransportMessage message)
    {
        // message.WriteDelimitedTo is a helper method that does this for us. But it uses a default buffer size of 4096
        // which is much bigger than we need - most messages are only a few bytes. A small buffer size ends up being
        // significantly faster on the ServerStreamingManyMessagesPerformance test.
        CodedOutputStream codedOutput = new CodedOutputStream(_packetBuffer, 16);
        codedOutput.WriteLength(message.CalculateSize());
        message.WriteTo(codedOutput);
        codedOutput.Flush();
        return this;
    }

    public WriteTransaction RequestInit(string methodFullName, DateTime? deadline)
    {
        _logger.Log($"Sending <RequestInit> for '{methodFullName}'");
        return AddMessage(new TransportMessage
        {
            RequestInit = new RequestInit
            {
                MethodFullName = methodFullName,
                Deadline = deadline != null ? Timestamp.FromDateTime(deadline.Value) : null,
                ConnectionId = _logger.ConnectionId
            }
        });
    }

    private void ToTransportMetadata(Metadata metadata, RepeatedField<MetadataEntry> transportMetadata)
    {
        foreach (var entry in metadata ?? new Metadata())
        {
            var transportEntry = new MetadataEntry
            {
                Name = entry.Key
            };
            if (entry.IsBinary)
            {
                transportEntry.ValueBytes = ByteString.CopyFrom(entry.ValueBytes);
            }
            else
            {
                transportEntry.ValueString = entry.Value;
            }

            transportMetadata.Add(transportEntry);
        }
    }

    public WriteTransaction Headers(Metadata headers)
    {
        _logger.Log($"Sending <Headers>");
        var transportHeaders = new Headers();
        ToTransportMetadata(headers, transportHeaders.Metadata);
        return AddMessage(new TransportMessage
        {
            Headers = transportHeaders
        });
    }

    public WriteTransaction Trailers(StatusCode statusCode, string statusDetail, Metadata trailers)
    {
        _logger.Log($"Sending <Trailers> with status '{statusCode}'");
        var transportTrailers = new Trailers
        {
            StatusCode = (int) statusCode,
            StatusDetail = statusDetail
        };
        ToTransportMetadata(trailers, transportTrailers.Metadata);
        return AddMessage(new TransportMessage
        {
            Trailers = transportTrailers
        });
    }

    public WriteTransaction Cancel()
    {
        _logger.Log("Sending <Cancel>");
        return AddMessage(new TransportMessage
        {
            RequestControl = RequestControl.Cancel
        });
    }

    public WriteTransaction RequestStreamEnd()
    {
        _logger.Log("Sending <StreamEnd>");
        return AddMessage(new TransportMessage
        {
            RequestControl = RequestControl.StreamEnd
        });
    }

    public WriteTransaction Payload(byte[] payload)
    {
        _logger.Log($"Sending <PayloadInfo> with {payload.Length} bytes");
        // TODO: Why doesn't this work on Unix?
        if (payload.Length > PayloadInSeparatePacketThreshold &&
            Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            // For large payloads, writing the payload outside the packet saves extra copying.
            AddMessage(new TransportMessage
            {
                PayloadInfo = new PayloadInfo
                {
                    Size = payload.Length,
                    InSamePacket = false
                }
            });
            _trailingPayloads.Add(payload);
        }
        else
        {
            // For small payloads, including the payload in the packet reduces the number of reads.
            AddMessage(new TransportMessage
            {
                PayloadInfo = new PayloadInfo
                {
                    Size = payload.Length,
                    InSamePacket = true
                }
            });
            _packetBuffer.Write(payload, 0, payload.Length);
        }

        return this;
    }
}