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

namespace GrpcDotNetNamedPipes;

/// <summary>
/// A subclass of ServerCallContext for calls to a NamedPipeServer.
///
/// You only need to use this class in order to call RunAsClient for impersonation.
/// The client needs to set NamedPipeChannelOptions.ImpersonationLevel for this to work.
/// </summary>
/// <example>
/// <code>
/// public override Task&lt;HelloResponse&gt; SayHello(HelloRequest request, ServerCallContext context)
/// {
///     var namedPipeCallContext = (NamedPipeCallContext) context;
///     namedPipeCallContext.RunAsClient(DoSomething);
///     return new HelloResponse();
/// }
/// </code>
/// </example>
public class NamedPipeCallContext : ServerCallContext
{
    private readonly ServerConnectionContext _ctx;

    internal NamedPipeCallContext(ServerConnectionContext ctx)
    {
        _ctx = ctx;
    }

    /// <summary>
    /// Calls a delegate while impersonating the client.
    ///
    /// The client needs to set NamedPipeChannelOptions.ImpersonationLevel for this to work.
    /// </summary>
    public void RunAsClient(PipeStreamImpersonationWorker impersonationWorker)
    {
        _ctx.PipeStream.RunAsClient(impersonationWorker);
    }

    internal void DisconnectPipeStream()
    {
        _ctx.PipeStream.Disconnect();
    }

    protected override CancellationToken CancellationTokenCore =>
        _ctx.CancellationTokenSource.Token;

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
    {
        _ctx.Transport.Write().Headers(responseHeaders).Commit();
        return Task.CompletedTask;
    }

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) =>
        throw new NotSupportedException();

    protected override string MethodCore => throw new NotSupportedException();

    protected override string HostCore => throw new NotSupportedException();

    /// <summary>
    /// Returns a string in the form "net.pipe://localhost/pid/12345" that gives you the machine name (or "localhost")
    /// and the process ID of the caller for the current RPC.
    /// </summary>
    /// <exception cref="NotSupportedException">Only supported on Windows.</exception>
    protected override string PeerCore
    {
        get
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new NotSupportedException();
            var pipeHandle = _ctx.PipeStream.SafePipeHandle.DangerousGetHandle();
            var computerName = PipeInterop.GetClientComputerName(pipeHandle);
            var processId = PipeInterop.GetClientProcessId(pipeHandle);
            return $"net.pipe://{computerName}/pid/{processId}";
        }
    }

    protected override DateTime DeadlineCore => _ctx.Deadline.Value;

    protected override Metadata RequestHeadersCore => _ctx.RequestHeaders;

    protected override Metadata ResponseTrailersCore { get; } = new();

    protected override Status StatusCore { get; set; }

    protected override WriteOptions WriteOptionsCore
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    protected override AuthContext AuthContextCore => throw new NotSupportedException();
}