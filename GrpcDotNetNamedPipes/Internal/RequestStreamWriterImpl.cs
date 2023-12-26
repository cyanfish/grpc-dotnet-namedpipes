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

internal class RequestStreamWriterImpl<T> : StreamWriterImpl<T>, IClientStreamWriter<T>
{
    private readonly Task _initTask;
    private bool _isInitialized;
    private bool _isCompleted;

    public RequestStreamWriterImpl(NamedPipeTransport stream, CancellationToken cancellationToken,
        Marshaller<T> marshaller, Task initTask)
        : base(stream, cancellationToken, marshaller)
    {
        _initTask = initTask;
    }

    public override Task WriteAsync(T message)
    {
        if (_isCompleted)
        {
            throw new InvalidOperationException($"Request stream has already been completed.");
        }
        return WriteAsyncCore(message);
    }

    private async Task WriteAsyncCore(T message)
    {
        if (!_isInitialized)
        {
            await _initTask.ConfigureAwait(false);
            _isInitialized = true;
        }
        await base.WriteAsync(message).ConfigureAwait(false);
    }

    public async Task CompleteAsync()
    {
        if (!_isInitialized)
        {
            await _initTask.ConfigureAwait(false);
            _isInitialized = true;
        }
        if (CancelToken.IsCancellationRequested)
        {
            throw new TaskCanceledException();
        }
        Stream.Write().RequestStreamEnd().Commit();
        _isCompleted = true;
    }
}