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

internal class ClientConnectionContext : TransportMessageHandler, IDisposable
{
    private readonly NamedPipeClientStream _pipeStream;
    private readonly CallOptions _callOptions;
    private readonly bool _isServerUnary;
    private readonly PayloadQueue _payloadQueue;
    private readonly Deadline _deadline;
    private readonly int _connectionTimeout;
    private readonly ConnectionLogger _logger;

    private readonly TaskCompletionSource<Metadata> _responseHeadersTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private CancellationTokenRegistration _cancelReg;
    private byte[] _pendingPayload;
    private Metadata _responseTrailers;
    private Status _status;

    public ClientConnectionContext(NamedPipeClientStream pipeStream, CallOptions callOptions, bool isServerUnary,
        int connectionTimeout, ConnectionLogger logger)
    {
        _pipeStream = pipeStream;
        _callOptions = callOptions;
        _isServerUnary = isServerUnary;
        Transport = new NamedPipeTransport(pipeStream, logger);
        _payloadQueue = new PayloadQueue();
        _deadline = new Deadline(callOptions.Deadline);
        _connectionTimeout = connectionTimeout;
        _logger = logger;
    }

    public NamedPipeTransport Transport { get; }

    public Task<Metadata> ResponseHeadersAsync => _responseHeadersTcs.Task;

    public void InitCall<TRequest, TResponse>(Method<TRequest, TResponse> method, TRequest request)
    {
        if (_callOptions.CancellationToken.IsCancellationRequested || _deadline.IsExpired)
        {
            return;
        }

        _pipeStream.Connect(_connectionTimeout);
        _pipeStream.ReadMode = PlatformConfig.TransmissionMode;

        if (request != null)
        {
            var payload = SerializationHelpers.Serialize(method.RequestMarshaller, request);
            Transport.Write()
                .RequestInit(method.FullName, _callOptions.Deadline)
                .Headers(_callOptions.Headers)
                .Payload(payload)
                .Commit();
            _cancelReg = _callOptions.CancellationToken.Register(DisposeCall);
        }
        else
        {
            Transport.Write()
                .RequestInit(method.FullName, _callOptions.Deadline)
                .Headers(_callOptions.Headers)
                .Commit();
            _cancelReg = _callOptions.CancellationToken.Register(DisposeCall);
        }
    }

    public override void HandleHeaders(Metadata headers)
    {
        EnsureResponseHeadersSet(headers);
    }

    public override void HandleTrailers(Metadata trailers, Status status)
    {
        EnsureResponseHeadersSet();
        _responseTrailers = trailers ?? new Metadata();
        _status = status;

        if (_pendingPayload != null)
        {
            _payloadQueue.AppendPayload(_pendingPayload);
        }

        if (status.StatusCode == StatusCode.OK)
        {
            _payloadQueue.SetCompleted();
        }
        else
        {
            _payloadQueue.SetError(new RpcException(status));
        }
    }

    public override void HandlePayload(byte[] payload)
    {
        EnsureResponseHeadersSet();

        if (_isServerUnary)
        {
            // Wait to process the payload until we've received the trailers
            _pendingPayload = payload;
        }
        else
        {
            _payloadQueue.AppendPayload(payload);
        }
    }

    private void EnsureResponseHeadersSet(Metadata headers = null)
    {
        if (!_responseHeadersTcs.Task.IsCompleted)
        {
            _responseHeadersTcs.SetResult(headers ?? new Metadata());
        }
    }

    public Metadata GetTrailers() => _responseTrailers ?? throw new InvalidOperationException();

    public Status GetStatus() => _responseTrailers != null ? _status : throw new InvalidOperationException();

    public MessageReader<TResponse> GetMessageReader<TResponse>(Marshaller<TResponse> responseMarshaller)
    {
        return new MessageReader<TResponse>(_payloadQueue, responseMarshaller, _callOptions.CancellationToken,
            _deadline);
    }

    public IClientStreamWriter<TRequest> CreateRequestStream<TRequest>(Marshaller<TRequest> requestMarshaller)
    {
        return new RequestStreamWriterImpl<TRequest>(Transport, _callOptions.CancellationToken, requestMarshaller);
    }

    public void DisposeCall()
    {
        try
        {
            Transport.Write().Cancel().Commit();
        }
        catch (Exception)
        {
            // Assume the connection is already terminated
        }
    }

    public void Dispose()
    {
        _cancelReg.Dispose();
        _payloadQueue.Dispose();
        _pipeStream.Dispose();
    }
}