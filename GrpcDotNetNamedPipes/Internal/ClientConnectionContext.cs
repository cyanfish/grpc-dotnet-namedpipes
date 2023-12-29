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
    private readonly SimpleAsyncLock _connectLock;
    private readonly ConnectionLogger _logger;

    private readonly TaskCompletionSource<Metadata> _responseHeadersTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _cancelReg;
    private byte[] _pendingPayload;
    private Metadata _responseTrailers;
    private Status _status;

    public ClientConnectionContext(NamedPipeClientStream pipeStream, CallOptions callOptions, bool isServerUnary,
        int connectionTimeout, SimpleAsyncLock connectLock, ConnectionLogger logger)
    {
        _pipeStream = pipeStream;
        _callOptions = callOptions;
        _isServerUnary = isServerUnary;
        Transport = new NamedPipeTransport(pipeStream, logger);
        _payloadQueue = new PayloadQueue();
        _deadline = new Deadline(callOptions.Deadline);
        _connectionTimeout = connectionTimeout;
        _connectLock = connectLock;
        _logger = logger;
    }

    public Task InitTask { get; private set; }

    public NamedPipeTransport Transport { get; }

    public Task<Metadata> ResponseHeadersAsync => _responseHeadersTcs.Task;

    public void InitCall<TRequest, TResponse>(Method<TRequest, TResponse> method, TRequest request)
    {
        if (_callOptions.CancellationToken.IsCancellationRequested || _deadline.IsExpired)
        {
            InitTask = Task.CompletedTask;
            return;
        }

        InitTask = Task.Run(async () =>
        {
            try
            {
                await ConnectPipeWithRetries();
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
            catch (Exception ex)
            {
                _pipeStream.Dispose();

                if (ex is TimeoutException || ex is IOException)
                {
                    _payloadQueue.SetError(new RpcException(new Status(StatusCode.Unavailable, "failed to connect to all addresses")));
                }
                else
                {
                    _payloadQueue.SetError(ex);
                }
            }
        });
    }

    private async Task ConnectPipeWithRetries()
    {
        // In theory .Connect is supposed to have a timeout and not need retries, but it turns
        // out that in practice sometimes it does.
        var elapsed = Stopwatch.StartNew();
        int TimeLeft() => Math.Max(_connectionTimeout - (int) elapsed.ElapsedMilliseconds, 0);
        var fallback = 100;
        while (true)
        {
            try
            {
                await _connectLock.Take();
                var waitTime = _connectionTimeout == -1 ? 1000 : Math.Min(1000, TimeLeft());
                await _pipeStream.ConnectAsync(waitTime).ConfigureAwait(false);
                _connectLock.Release();
                break;
            }
            catch (Exception ex)
            {
                _connectLock.Release();
                if (ex is not (TimeoutException or IOException))
                {
                    throw;
                }
                if (_connectionTimeout != -1 && TimeLeft() == 0)
                {
                    throw;
                }
                var delayTime = _connectionTimeout == -1 ? fallback : Math.Min(fallback, TimeLeft());
                await Task.Delay(delayTime);
                fallback = Math.Min(fallback * 2, 1000);
            }
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
            _payloadQueue.SetError(new RpcException(status, _responseTrailers));
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
        return new RequestStreamWriterImpl<TRequest>(Transport, _callOptions.CancellationToken, requestMarshaller,
            InitTask);
    }

    public void DisposeCall()
    {
        InitTask.ContinueWith(_ =>
        {
            try
            {
                Transport.Write().Cancel().Commit();
            }
            catch (Exception)
            {
                // Assume the connection is already terminated
            }
        }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    public void Dispose()
    {
        _cancelReg.Dispose();
        _payloadQueue.Dispose();
        _pipeStream.Dispose();
    }
}