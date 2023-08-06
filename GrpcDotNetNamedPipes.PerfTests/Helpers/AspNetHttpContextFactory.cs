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

#if NET6_0_OR_GREATER
using System.Net.Http;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GrpcDotNetNamedPipes.PerfTests.Helpers;

public class AspNetHttpContextFactory : ChannelContextFactory
{
    private int _port;

    public override ChannelContext Create(ITestOutputHelper output = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGrpc(opts => opts.MaxReceiveMessageSize = int.MaxValue);
        builder.WebHost.UseUrls("https://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(opts =>
        {
            opts.Limits.MaxRequestBodySize = int.MaxValue;
            opts.ConfigureEndpointDefaults(c => c.Protocols = HttpProtocols.Http2);
        });
        var app = builder.Build();
        app.MapGrpcService<TestServiceImpl>();
        app.Start();
        var server = app.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        foreach (var address in addressFeature.Addresses)
        {
            _port = int.Parse(address.Split(":").Last());
        }

        return new ChannelContext
        {
            Impl = new TestServiceImpl(), // TODO: Match instance
            Client = CreateClient(output),
            OnDispose = () => app.StopAsync()
        };
    }

    public override TestService.TestServiceClient CreateClient(ITestOutputHelper output = null)
    {
        var httpHandler = new HttpClientHandler();
        httpHandler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        return new TestService.TestServiceClient(GrpcChannel.ForAddress($"https://127.0.0.1:{_port}",
            new GrpcChannelOptions { HttpHandler = httpHandler, MaxReceiveMessageSize = int.MaxValue }));
    }

    public override string ToString()
    {
        return "aspnet-http";
    }
}
#endif