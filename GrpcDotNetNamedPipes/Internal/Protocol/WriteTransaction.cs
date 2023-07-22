using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace GrpcDotNetNamedPipes.Internal.Protocol;

internal class WriteTransaction
{
    private const int PayloadInSeparatePacketThreshold = 15 * 1024; // 15 kiB

    private readonly PipeStream _pipeStream;
    private readonly MemoryStream _packetBuffer = new MemoryStream();
    private readonly List<byte[]> _trailingPayloads = new List<byte[]>();

    public WriteTransaction(PipeStream pipeStream)
    {
        _pipeStream = pipeStream;
    }

    private WriteTransaction AddMessage(TransportMessage message)
    {
        message.WriteDelimitedTo(_packetBuffer);
        return this;
    }

    public void Commit()
    {
        lock (_pipeStream)
        {
            if (_packetBuffer.Length > 0)
            {
                if (PlatformConfig.SizePrefix)
                {
                    _pipeStream.Write(BitConverter.GetBytes(_packetBuffer.Length), 0, 4);
                }
                _packetBuffer.WriteTo(_pipeStream);
            }

            foreach (var payload in _trailingPayloads)
            {
                _pipeStream.Write(payload, 0, payload.Length);
            }
        }
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