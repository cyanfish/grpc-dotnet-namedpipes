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

#if NET8_0_OR_GREATER
using System.Net.Http;
using System.Security.Principal;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GrpcDotNetNamedPipes.PerfTests.Helpers;

public class AspNetPipeContextFactory : ChannelContextFactory
{
    private string _pipe;

    public override ChannelContext Create(ITestOutputHelper output = null)
    {
        _pipe = $"pipe/{Guid.NewGuid()}";
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGrpc(opts => opts.MaxReceiveMessageSize = int.MaxValue);
        builder.WebHost.UseUrls("https://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(opts =>
        {
            opts.Limits.MaxRequestBodySize = int.MaxValue;
            opts.ListenNamedPipe(_pipe, c => c.Protocols = HttpProtocols.Http2);
        });
        var app = builder.Build();
        app.MapGrpcService<TestServiceImpl>();
        app.Start();

        return new ChannelContext
        {
            Impl = new TestServiceImpl(), // TODO: Match instance
            Client = CreateClient(output),
            OnDispose = () => app.StopAsync()
        };
    }

    public override TestService.TestServiceClient CreateClient(ITestOutputHelper output = null)
    {
        var connectionFactory = new NamedPipesConnectionFactory(_pipe);
        var socketsHttpHandler = new SocketsHttpHandler
        {
            ConnectCallback = connectionFactory.ConnectAsync
        };
        return new TestService.TestServiceClient(GrpcChannel.ForAddress("http://localhost",
            new GrpcChannelOptions { HttpHandler = socketsHttpHandler, MaxReceiveMessageSize = int.MaxValue }));
    }

    public override string ToString()
    {
        return "aspnet-pipe";
    }

    private class NamedPipesConnectionFactory
    {
        private readonly string pipeName;

        public NamedPipesConnectionFactory(string pipeName)
        {
            this.pipeName = pipeName;
        }

        public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _,
            CancellationToken cancellationToken = default)
        {
            var clientStream = new NamedPipeClientStream(
                serverName: ".",
                pipeName: this.pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.WriteThrough | PipeOptions.Asynchronous,
                impersonationLevel: TokenImpersonationLevel.Anonymous);

            try
            {
                await clientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);
                return clientStream;
            }
            catch
            {
                clientStream.Dispose();
                throw;
            }
        }
    }
}
#endif