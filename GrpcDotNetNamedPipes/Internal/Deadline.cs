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

internal class Deadline
{
    private readonly DateTime? _deadline;
    private readonly CancellationTokenSource _cts;

    public Deadline(DateTime? deadline)
    {
        _deadline = deadline;
        _cts = new CancellationTokenSource();

        if (_deadline != null)
        {
            long millis = (long) (_deadline.Value - DateTime.UtcNow).TotalMilliseconds;
            if (millis <= 0)
            {
                _cts.Cancel();
            }
            else if (millis < int.MaxValue)
            {
                _cts.CancelAfter((int) millis);
            }
        }
    }

    public DateTime Value => _deadline ?? DateTime.MaxValue;

    public CancellationToken Token => _cts.Token;

    public bool IsExpired => _cts.IsCancellationRequested;
}