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

public class NamedPipeChannelContextFactory : ChannelContextFactory
{
    private readonly string _pipeName = $"{Guid.NewGuid().ToString().Replace("-", "")}";
    private const int _connectionTimeout = 100;

    public ChannelContext Create(NamedPipeServerOptions options, ITestOutputHelper output)
    {
        var impl = new TestServiceImpl();
        var server = new NamedPipeServer(_pipeName, options, output != null ? output.WriteLine : null);
        TestService.BindService(server.ServiceBinder, impl);
        server.Start();
        return new ChannelContext
        {
            Impl = impl,
            Client = CreateClient(output),
            OnDispose = () => server.Kill()
        };
    }

    public override ChannelContext Create(ITestOutputHelper output = null)
    {
        return Create(new NamedPipeServerOptions(), output);
    }

    public override TestService.TestServiceClient CreateClient(ITestOutputHelper output = null)
    {
        var channel = new NamedPipeChannel(".", _pipeName,
            new NamedPipeChannelOptions { ConnectionTimeout = _connectionTimeout },
            output != null ? output.WriteLine : null);
        channel.PipeCallback = PipeCallback;
        return new TestService.TestServiceClient(channel);
    }

    public Action<NamedPipeClientStream> PipeCallback { get; set; }

    public NamedPipeServer CreateServer() => new(_pipeName, new NamedPipeServerOptions());

    public override string ToString()
    {
        return "pipe";
    }
}