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

namespace GrpcDotNetNamedPipes.Internal.Helpers;

internal class ConnectionLogger
{
    private static int _lastId;

    private static int NextId() => Interlocked.Increment(ref _lastId);
    public static ConnectionLogger Client(Action<string> log) => new(log, "CLIENT", log != null ? NextId() : 0);
    public static ConnectionLogger Server(Action<string> log) => new(log, "SERVER", 0);

    private readonly Action<string> _log;
    private readonly string _type;

    private ConnectionLogger(Action<string> log, string type, int id)
    {
        _log = log;
        _type = type;
        ConnectionId = id;
    }

    public int ConnectionId { get; set; }

    public void Log(string message)
    {
        if (_log == null) return;
        var id = ConnectionId > 0 ? ConnectionId.ToString() : "?";
        _log($"[{_type}][{id}] {message}");
    }
}