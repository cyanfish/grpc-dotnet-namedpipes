using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace GrpcDotNetNamedPipes.Internal.Protocol;

internal class WriteTransaction
{
    private const int PayloadInSeparatePacketThreshold = 15 * 1024; // 15 kiB

    private readonly WriteTransactionQueue _txQueue;
    private readonly MemoryStream _packetBuffer = new();
    private readonly List<byte[]> _trailingPayloads = new();

    public WriteTransaction(WriteTransactionQueue txQueue)
    {
        _txQueue = txQueue;
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
        message.WriteDelimitedTo(_packetBuffer);
        return this;
    }

    public WriteTransaction RequestInit(string methodFullName, DateTime? deadline)
    {
        return AddMessage(new TransportMessage
        {
            RequestInit = new RequestInit
            {
                MethodFullName = methodFullName,
                Deadline = deadline != null ? Timestamp.FromDateTime(deadline.Value) : null
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
        var transportHeaders = new Headers();
        ToTransportMetadata(headers, transportHeaders.Metadata);
        return AddMessage(new TransportMessage
        {
            Headers = transportHeaders
        });
    }

    public WriteTransaction Trailers(StatusCode statusCode, string statusDetail, Metadata trailers)
    {
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
        return AddMessage(new TransportMessage
        {
            RequestControl = RequestControl.Cancel
        });
    }

    public WriteTransaction RequestStreamEnd()
    {
        return AddMessage(new TransportMessage
        {
            RequestControl = RequestControl.StreamEnd
        });
    }

    public WriteTransaction Payload(byte[] payload)
    {
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