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

using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace GrpcDotNetNamedPipes.Internal
{
    internal class StreamWriterImpl<T> : IClientStreamWriter<T>, IServerStreamWriter<T>
    {
        private readonly NamedPipeTransport _stream;
        private readonly CancellationToken _cancellationToken;
        private readonly Marshaller<T> _marshaller;

        public StreamWriterImpl(NamedPipeTransport stream, CancellationToken cancellationToken, Marshaller<T> marshaller)
        {
            _stream = stream;
            _cancellationToken = cancellationToken;
            _marshaller = marshaller;
        }

        public WriteOptions WriteOptions { get; set; }

        public Task WriteAsync(T message)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(_cancellationToken);
            }

            var payload = _marshaller.Serializer(message);
            _stream.Write().Payload(payload).Commit();
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            _stream.Write().RequestStreamEnd().Commit();
            return Task.CompletedTask;
        }
    }
}