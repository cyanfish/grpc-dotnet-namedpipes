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

namespace GrpcDotNetNamedPipes.Tests.Helpers;

public class HttpChannelContextFactory : ChannelContextFactory
{
    private static readonly ChannelOption[] Options =
    {
        new(ChannelOptions.MaxReceiveMessageLength, 1024 * 1024 * 1024),
        new(ChannelOptions.MaxSendMessageLength, 1024 * 1024 * 1024)
    };
    private int _port;

    public override ChannelContext Create(ITestOutputHelper output = null)
    {
        var impl = new TestServiceImpl();
        var server = new Server(Options)
        {
            Services = { TestService.BindService(impl) },
            Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
        };
        server.Start();
        _port = server.Ports.First().BoundPort;
        return new ChannelContext
        {
            Impl = impl,
            Client = CreateClient(output),
            OnDispose = () => server.KillAsync()
        };
    }

    public override TestService.TestServiceClient CreateClient(ITestOutputHelper output = null)
    {
        var channel = new Channel(
            "localhost",
            _port,
            ChannelCredentials.Insecure,
            Options);
        return new TestService.TestServiceClient(channel);
    }

    public override string ToString()
    {
        return "http";
    }
}