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

using System;
using GrpcDotNetNamedPipes.Tests.Generated;

namespace GrpcDotNetNamedPipes.Tests.Helpers
{
    public class NamedPipeChannelContextFactory : ChannelContextFactory
    {
        private readonly string _pipeName = $"GrpcNamedPipeTests/{Guid.NewGuid()}";
        private const int _connectionTimeout = 100;
            
        public ChannelContext Create(NamedPipeServerOptions options)
        {
            var impl = new TestServiceImpl();
            var server = new NamedPipeServer(_pipeName, options);
            TestService.BindService(server.ServiceBinder, impl);
            server.Start();
            return new ChannelContext
            {
                Impl = impl,
                Client = CreateClient(),
                OnDispose = () => server.Kill()
            };
        }

        public override ChannelContext Create()
        {
            return Create(new NamedPipeServerOptions());
        }

        public override TestService.TestServiceClient CreateClient()
        {
            var channel = new NamedPipeChannel(".", _pipeName, new NamedPipeChannelOptions{ ConnectionTimeout = _connectionTimeout });
            return new TestService.TestServiceClient(channel);
        }

        public override string ToString()
        {
            return "pipe";
        }
    }
}