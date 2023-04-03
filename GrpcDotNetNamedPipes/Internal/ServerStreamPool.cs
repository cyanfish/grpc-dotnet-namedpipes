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
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcDotNetNamedPipes.Internal
{
    internal class ServerStreamPool : IDisposable
    {
        private const int PoolSize = 4;
        private const int FallbackMin = 100;
        private const int FallbackMax = 10_000;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly string _pipeName;
        private readonly NamedPipeServerOptions _options;
        private readonly Func<NamedPipeServerStream, Task> _handleConnection;
        private readonly Action<Exception> _invokeError;
        private bool _started;
        private bool _stopped;

        public ServerStreamPool(string pipeName, NamedPipeServerOptions options,
            Func<NamedPipeServerStream, Task> handleConnection, Action<Exception> invokeError)
        {
            _pipeName = pipeName;
            _options = options;
            _handleConnection = handleConnection;
            _invokeError = invokeError;
        }

        private NamedPipeServerStream CreatePipeServer()
        {
            var pipeOptions = PipeOptions.Asynchronous;
#if NETFRAMEWORK
            return new NamedPipeServerStream(_pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PlatformConfig.TransmissionMode,
                pipeOptions,
                0,
                0,
                _options.PipeSecurity);
#else
#if NET6_0_OR_GREATER
            if (_options.CurrentUserOnly)
            {
                pipeOptions |= PipeOptions.CurrentUserOnly;
            }
            if (OperatingSystem.IsWindows())
            {
                return NamedPipeServerStreamAcl.Create(_pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PlatformConfig.TransmissionMode,
                    pipeOptions,
                    0,
                    0,
                    _options.PipeSecurity);
            }
#endif
            return new NamedPipeServerStream(_pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PlatformConfig.TransmissionMode,
                pipeOptions);
#endif
        }

        public void Start()
        {
            if (_stopped)
            {
                throw new InvalidOperationException(
                    "The server has been killed and can't be restarted. Create a new server if needed.");
            }
            if (_started)
            {
                return;
            }

            for (int i = 0; i < PoolSize; i++)
            {
                StartListenThread();
            }

            _started = true;
        }

        private void StartListenThread()
        {
            var _ = ConnectionLoop();
        }

        private async Task ConnectionLoop()
        {
            int fallback = FallbackMin;
            while (true)
            {
                try
                {
                    await ListenForConnection().ConfigureAwait(false);
                    fallback = FallbackMin;
                }
                catch (Exception error)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }
                    _invokeError(error);
                    await Task.Delay(fallback).ConfigureAwait(false);
                    fallback = Math.Min(fallback * 2, FallbackMax);
                }
            }
        }

        private async Task ListenForConnection()
        {
            var pipeServer = CreatePipeServer();

            await WaitForConnection(pipeServer).ConfigureAwait(false);

            RunHandleConnection(pipeServer);
        }

        private async Task WaitForConnection(NamedPipeServerStream pipeServer)
        {
            try
            {
                await pipeServer.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                try
                {
                    pipeServer.Disconnect();
                }
                catch (Exception)
                {
                    // Ignore disconnection errors
                }
                pipeServer.Dispose();
                throw;
            }
        }

        private void RunHandleConnection(NamedPipeServerStream pipeServer)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _handleConnection(pipeServer);
                    pipeServer.Disconnect();
                }
                catch (Exception error)
                {
                    _invokeError(error);
                }
                finally
                {
                    pipeServer.Dispose();
                }
            });
        }

        public void Dispose()
        {
            _stopped = true;
            try
            {
                _cts.Cancel();
            }
            catch (Exception error)
            {
                _invokeError(error);
            }
        }
    }
}