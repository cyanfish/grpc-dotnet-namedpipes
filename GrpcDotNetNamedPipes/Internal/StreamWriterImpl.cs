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

internal class StreamWriterImpl<T> : IAsyncStreamWriter<T>
{
    private readonly CancellationToken _cancellationToken;
    private readonly Marshaller<T> _marshaller;

    public StreamWriterImpl(NamedPipeTransport stream, CancellationToken cancellationToken, Marshaller<T> marshaller)
    {
        Stream = stream;
        _cancellationToken = cancellationToken;
        _marshaller = marshaller;
    }

    public WriteOptions WriteOptions { get; set; }

    protected NamedPipeTransport Stream { get; }

    public virtual Task WriteAsync(T message)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(_cancellationToken);
        }

        var payload = SerializationHelpers.Serialize(_marshaller, message);
        // TODO: Potential for 4x streaming message throughput by queueing up messages and sending multiple at a time
        Stream.Write().Payload(payload).Commit();
        return Task.CompletedTask;
    }
}