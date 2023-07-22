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

internal class PayloadQueue : IAsyncStreamReader<byte[]>
{
    private readonly Queue<byte[]> _internalQueue = new();
    private TaskCompletionSource<bool> _tcs;
    private CancellationTokenRegistration _cancelReg;
    private Exception _error;
    private bool _completed;
    private bool _terminated;

    public void AppendPayload(byte[] payload)
    {
        lock (this)
        {
            _internalQueue.Enqueue(payload);
            if (_tcs != null)
            {
                Current = _internalQueue.Dequeue();
                _tcs.SetResult(true);
                ResetTcs();
            }
        }
    }

    private void ResetTcs()
    {
        _cancelReg.Dispose();
        _tcs = null;
    }

    public void SetCompleted()
    {
        lock (this)
        {
            _terminated = true;
            _completed = true;
            if (_tcs != null)
            {
                _tcs.SetResult(false);
                ResetTcs();
            }
        }
    }

    public void SetError(Exception ex)
    {
        lock (this)
        {
            _terminated = true;
            _error = ex;
            if (_tcs != null)
            {
                _tcs.SetException(_error);
                ResetTcs();
            }
        }
    }

    public void SetCanceled()
    {
        lock (this)
        {
            _terminated = true;
            if (_tcs != null)
            {
                _tcs.SetCanceled();
                ResetTcs();
            }
        }
    }

    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        lock (this)
        {
            if (_tcs != null)
            {
                throw new InvalidOperationException("Overlapping MoveNext calls");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }

            if (_internalQueue.Count > 0)
            {
                Current = _internalQueue.Dequeue();
                return Task.FromResult(true);
            }

            if (_error != null)
            {
                throw _error;
            }

            if (_completed)
            {
                return Task.FromResult(false);
            }

            _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var task = _tcs.Task;
            _cancelReg = cancellationToken.Register(SetCanceled);
            return task;
        }
    }

    public byte[] Current { get; private set; }

    public void Dispose()
    {
        if (!_terminated)
        {
            // Unexpected pipe termination
            SetError(new RpcException(new Status(StatusCode.Unavailable, "failed to connect to all addresses")));
        }
    }
}