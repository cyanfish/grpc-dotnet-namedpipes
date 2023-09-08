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

namespace GrpcDotNetNamedPipes;

public class NamedPipeChannel : CallInvoker
{
    private readonly string _serverName;
    private readonly string _pipeName;
    private readonly NamedPipeChannelOptions _options;
    private readonly Action<string> _log;
    private readonly Action<string> _errorLog;

    public NamedPipeChannel(string serverName, string pipeName)
        : this(serverName, pipeName, new NamedPipeChannelOptions())
    {
    }

    public NamedPipeChannel(string serverName, string pipeName, NamedPipeChannelOptions options)
        : this(serverName, pipeName, options, null, null)
    {
    }

    public NamedPipeChannel(string serverName, string pipeName, NamedPipeChannelOptions options, Action<string> log, Action<string> errorLog)
    {
        _serverName = serverName;
        _pipeName = pipeName;
        _options = options;
        _log = log;
        _errorLog = errorLog;
    }

    internal Action<NamedPipeClientStream> PipeCallback { get; set; }

    private ClientConnectionContext CreateConnectionContext<TRequest, TResponse>(
        Method<TRequest, TResponse> method, CallOptions callOptions, TRequest request)
        where TRequest : class where TResponse : class
    {
        var pipeOptions = PipeOptions.Asynchronous;
#if NETCOREAPP || NETSTANDARD2_1
        if (_options.CurrentUserOnly)
        {
            pipeOptions |= PipeOptions.CurrentUserOnly;
        }
#endif

        var stream = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut,
            pipeOptions, _options.ImpersonationLevel, HandleInheritability.None);
        PipeCallback?.Invoke(stream);

        try
        {
            bool isServerUnary = method.Type == MethodType.Unary || method.Type == MethodType.ClientStreaming;
            var logger = ConnectionLogger.Client(_log, _errorLog);
            var ctx = new ClientConnectionContext(stream, callOptions, isServerUnary, _options.ConnectionTimeout,
                logger);
            ctx.InitCall(method, request);
            Task.Run(new PipeReader(stream, ctx, logger, ctx.Dispose).ReadLoop);
            return ctx;
        }
        catch (Exception ex)
        {
            stream.Dispose();

            if (ex is TimeoutException || ex is IOException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "failed to connect to all addresses"));
            }
            else
            {
                throw;
            }
        }
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
        string host, CallOptions callOptions, TRequest request)
    {
        var ctx = CreateConnectionContext(method, callOptions, request);
        return ctx.GetMessageReader(method.ResponseMarshaller).ReadNextMessage(callOptions.CancellationToken)
            .Result;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string host, CallOptions callOptions, TRequest request)
    {
        var ctx = CreateConnectionContext(method, callOptions, request);
        return new AsyncUnaryCall<TResponse>(
            ctx.GetMessageReader(method.ResponseMarshaller).ReadNextMessage(callOptions.CancellationToken),
            ctx.ResponseHeadersAsync,
            ctx.GetStatus,
            ctx.GetTrailers,
            ctx.DisposeCall);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string host, CallOptions callOptions,
        TRequest request)
    {
        var ctx = CreateConnectionContext(method, callOptions, request);
        return new AsyncServerStreamingCall<TResponse>(
            ctx.GetMessageReader(method.ResponseMarshaller),
            ctx.ResponseHeadersAsync,
            ctx.GetStatus,
            ctx.GetTrailers,
            ctx.DisposeCall);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string host, CallOptions callOptions)
    {
        var ctx = CreateConnectionContext(method, callOptions, null);
        return new AsyncClientStreamingCall<TRequest, TResponse>(
            ctx.CreateRequestStream(method.RequestMarshaller),
            ctx.GetMessageReader(method.ResponseMarshaller).ReadNextMessage(callOptions.CancellationToken),
            ctx.ResponseHeadersAsync,
            ctx.GetStatus,
            ctx.GetTrailers,
            ctx.DisposeCall);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string host, CallOptions callOptions)
    {
        var ctx = CreateConnectionContext(method, callOptions, null);
        return new AsyncDuplexStreamingCall<TRequest, TResponse>(
            ctx.CreateRequestStream(method.RequestMarshaller),
            ctx.GetMessageReader(method.ResponseMarshaller),
            ctx.ResponseHeadersAsync,
            ctx.GetStatus,
            ctx.GetTrailers,
            ctx.DisposeCall);
    }
}