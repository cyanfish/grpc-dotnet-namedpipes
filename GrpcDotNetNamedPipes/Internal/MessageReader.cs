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

internal class MessageReader<TMessage> : IAsyncStreamReader<TMessage>
{
    private readonly PayloadQueue _payloadQueue;
    private readonly Marshaller<TMessage> _marshaller;
    private readonly CancellationToken _callCancellationToken;
    private readonly Deadline _deadline;

    public MessageReader(PayloadQueue payloadQueue, Marshaller<TMessage> marshaller,
        CancellationToken callCancellationToken, Deadline deadline)
    {
        _payloadQueue = payloadQueue;
        _marshaller = marshaller;
        _callCancellationToken = callCancellationToken;
        _deadline = deadline;
    }

    public async Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        try
        {
            using var combined =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _callCancellationToken,
                    _deadline.Token);
            return await _payloadQueue.MoveNext(combined.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (_deadline.IsExpired)
            {
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, ""));
            }
            else
            {
                throw new RpcException(Status.DefaultCancelled);
            }
        }
    }

    public TMessage Current => SerializationHelpers.Deserialize(_marshaller, _payloadQueue.Current);

    public Task<TMessage> ReadNextMessage()
    {
        return ReadNextMessage(CancellationToken.None);
    }

    public Task<TMessage> ReadNextMessage(CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            if (!await MoveNext(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Expected payload");
            }

            return Current;
        });
    }
}
