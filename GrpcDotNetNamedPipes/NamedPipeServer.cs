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

public class NamedPipeServer : IDisposable
{
    private readonly ServerStreamPool _pool;
    private readonly Action<string> _log;
    private readonly Dictionary<string, Func<ServerConnectionContext, Task>> _methodHandlers = new();

    public NamedPipeServer(string pipeName)
        : this(pipeName, new NamedPipeServerOptions())
    {
    }

    public NamedPipeServer(string pipeName, NamedPipeServerOptions options)
        : this(pipeName, options, null)
    {
    }

    internal NamedPipeServer(string pipeName, NamedPipeServerOptions options, Action<string> log)
    {
        _pool = new ServerStreamPool(pipeName, options, HandleConnection, InvokeError);
        _log = log;
        ServiceBinder = new ServiceBinderImpl(this);
    }

    public ServiceBinderBase ServiceBinder { get; }

    public event EventHandler<NamedPipeErrorEventArgs> Error;

    private void InvokeError(Exception error)
    {
        Error?.Invoke(this, new NamedPipeErrorEventArgs(error));
    }

    public void Start()
    {
        _pool.Start();
    }

    public void Kill()
    {
        _pool.Dispose();
    }

    public void Dispose()
    {
        _pool.Dispose();
    }

    private async Task HandleConnection(NamedPipeServerStream pipeStream)
    {
        var logger = ConnectionLogger.Server(_log);
        var ctx = new ServerConnectionContext(pipeStream, logger, _methodHandlers);
        await Task.Run(new PipeReader(pipeStream, ctx, logger, ctx.Dispose, InvokeError).ReadLoop);
    }

    private class ServiceBinderImpl : ServiceBinderBase
    {
        private readonly NamedPipeServer _server;

        public ServiceBinderImpl(NamedPipeServer server)
        {
            _server = server;
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method,
            UnaryServerMethod<TRequest, TResponse> handler)
        {
            _server._methodHandlers.Add(method.FullName, async ctx =>
            {
                try
                {
                    var request = await ctx.GetMessageReader(method.RequestMarshaller).ReadNextMessage()
                        .ConfigureAwait(false);
                    var response = await handler(request, ctx.CallContext).ConfigureAwait(false);
                    ctx.Success(SerializationHelpers.Serialize(method.ResponseMarshaller, response));
                }
                catch (Exception ex)
                {
                    ctx.Error(ex);
                }
            });
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method,
            ClientStreamingServerMethod<TRequest, TResponse> handler)
        {
            _server._methodHandlers.Add(method.FullName, async ctx =>
            {
                try
                {
                    var response = await handler(
                        ctx.GetMessageReader(method.RequestMarshaller),
                        ctx.CallContext).ConfigureAwait(false);
                    ctx.Success(SerializationHelpers.Serialize(method.ResponseMarshaller, response));
                }
                catch (Exception ex)
                {
                    ctx.Error(ex);
                }
            });
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method,
            ServerStreamingServerMethod<TRequest, TResponse> handler)
        {
            _server._methodHandlers.Add(method.FullName, async ctx =>
            {
                try
                {
                    var request = await ctx.GetMessageReader(method.RequestMarshaller).ReadNextMessage()
                        .ConfigureAwait(false);
                    await handler(
                        request,
                        ctx.CreateResponseStream(method.ResponseMarshaller),
                        ctx.CallContext).ConfigureAwait(false);
                    ctx.Success();
                }
                catch (Exception ex)
                {
                    ctx.Error(ex);
                }
            });
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method,
            DuplexStreamingServerMethod<TRequest, TResponse> handler)
        {
            _server._methodHandlers.Add(method.FullName, async ctx =>
            {
                try
                {
                    await handler(
                        ctx.GetMessageReader(method.RequestMarshaller),
                        ctx.CreateResponseStream(method.ResponseMarshaller),
                        ctx.CallContext).ConfigureAwait(false);
                    ctx.Success();
                }
                catch (Exception ex)
                {
                    ctx.Error(ex);
                }
            });
        }
    }
}