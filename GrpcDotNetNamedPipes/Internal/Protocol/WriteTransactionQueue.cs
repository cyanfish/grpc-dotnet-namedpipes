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

namespace GrpcDotNetNamedPipes.Internal.Protocol;

internal class WriteTransactionQueue
{
    private readonly PipeStream _pipeStream;
    private List<WriteTransaction> _queue = new();
    private Task _dequeueTask;

    public WriteTransactionQueue(PipeStream pipeStream)
    {
        _pipeStream = pipeStream;
    }

    public void Add(WriteTransaction tx)
    {
        lock (this)
        {
            if (!_pipeStream.IsConnected)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "connection was unexpectedly terminated"));
            }
            _queue.Add(tx);
            _dequeueTask ??= Task.Run(Dequeue);
        }
    }

    private void Dequeue()
    {
        while (true)
        {
            List<WriteTransaction> transactionsToWrite;
            lock (this)
            {
                transactionsToWrite = _queue;
                _queue = new List<WriteTransaction>();
            }
            // Merge transactions together if multiple are queued
            var mergedTx = new WriteTransaction(this, null);
            foreach (var tx in transactionsToWrite)
            {
                mergedTx.MergeFrom(tx);
            }
            try
            {
                mergedTx.WriteTo(_pipeStream);
            }
            catch (Exception)
            {
                // Not a lot we can do to recover here
            }
            lock (this)
            {
                if (_queue.Count == 0)
                {
                    _dequeueTask = null;
                    break;
                }
            }
        }
    }
}