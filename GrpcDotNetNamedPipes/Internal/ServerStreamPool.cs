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
        private bool _started;
        private bool _stopped;

        public ServerStreamPool(string pipeName, NamedPipeServerOptions options,
            Func<NamedPipeServerStream, Task> handleConnection)
        {
            _pipeName = pipeName;
            _options = options;
            _handleConnection = handleConnection;
        }

        private NamedPipeServerStream CreatePipeServer()
        {
            var pipeOptions = PipeOptions.Asynchronous;
#if NETCOREAPP || NETSTANDARD
#if !NETSTANDARD2_0
            if (_options.CurrentUserOnly)
            {
                pipeOptions |= PipeOptions.CurrentUserOnly;
            }
#endif

#if NET5_0
            return NamedPipeServerStreamAcl.Create(_pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                pipeOptions,
                0,
                0,
                _options.PipeSecurity);
#else
            return new NamedPipeServerStream(_pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                pipeOptions);
#endif
#endif
#if NETFRAMEWORK
            return new NamedPipeServerStream(_pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                pipeOptions,
                0,
                0,
                _options.PipeSecurity);
#endif
        }

        public void Start()
        {
            if (_stopped)
            {
                throw new InvalidOperationException("The server has been killed and can't be restarted. Create a new server if needed.");
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
            var thread = new Thread(ConnectionLoop);
            thread.Start();
        }

        private void ConnectionLoop()
        {
            int fallback = FallbackMin;
            while (true)
            {
                try
                {
                    ListenForConnection();
                    fallback = FallbackMin;
                }
                catch (Exception)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }
                    // TODO: Log
                    Thread.Sleep(fallback);
                    fallback = Math.Min(fallback * 2, FallbackMax);
                }
            }
        }

        private void ListenForConnection()
        {
            var pipeServer = CreatePipeServer();
            WaitForConnection(pipeServer);
            RunHandleConnection(pipeServer);
        }

        private void WaitForConnection(NamedPipeServerStream pipeServer)
        {
            try
            {
                pipeServer.WaitForConnectionAsync(_cts.Token).Wait();
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
            Task.Run(() =>
            {
                try
                {
                    _handleConnection(pipeServer).Wait();
                    pipeServer.Disconnect();
                }
                catch (Exception)
                {
                    // TODO: Log
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
            catch (Exception)
            {
                // TODO: Log
            }
        }
    }
}