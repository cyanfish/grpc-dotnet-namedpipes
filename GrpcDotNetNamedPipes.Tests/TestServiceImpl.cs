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

namespace GrpcDotNetNamedPipes.Tests;

public class TestServiceImpl : TestService.TestServiceBase
{
    public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("Test exception");

    public bool SimplyUnaryCalled { get; private set; }

    public IServerStreamWriter<ResponseMessage> ServerStream { get; private set; }

    public override Task<ResponseMessage> SimpleUnary(RequestMessage request, ServerCallContext context)
    {
        SimplyUnaryCalled = true;
        return Task.FromResult(new ResponseMessage
        {
            Value = request.Value,
            Binary = request.Binary
        });
    }

    public override async Task<ResponseMessage> DelayedUnary(RequestMessage request, ServerCallContext context)
    {
        await Task.Delay(2000, context.CancellationToken);
        return new ResponseMessage();
    }

    public override Task<ResponseMessage> ThrowingUnary(RequestMessage request, ServerCallContext context)
    {
        context.ResponseTrailers.Add("test_key", "test_value");
        throw ExceptionToThrow;
    }

    public override async Task<ResponseMessage> DelayedThrowingUnary(RequestMessage request,
        ServerCallContext context)
    {
        await Task.Delay(2000, context.CancellationToken);
        throw ExceptionToThrow;
    }

    public override async Task<ResponseMessage> ClientStreaming(IAsyncStreamReader<RequestMessage> requestStream,
        ServerCallContext context)
    {
        int total = 0;
        while (await requestStream.MoveNext())
        {
            total += requestStream.Current.Value;
        }

        return new ResponseMessage { Value = total };
    }

    public override async Task ServerStreaming(RequestMessage request,
        IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
    {
        ServerStream = responseStream;
        for (int i = request.Value; i > 0; i--)
        {
            await responseStream.WriteAsync(new ResponseMessage { Value = i });
        }
    }

    public override async Task DelayedServerStreaming(RequestMessage request,
        IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
    {
        for (int i = request.Value; i > 0; i--)
        {
            await responseStream.WriteAsync(new ResponseMessage { Value = i });
            await Task.Delay(2000, context.CancellationToken);
            if (context.CancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public override async Task ThrowingServerStreaming(RequestMessage request,
        IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
    {
        ServerStream = responseStream;
        for (int i = request.Value; i > 0; i--)
        {
            await responseStream.WriteAsync(new ResponseMessage { Value = i });
        }
        throw new Exception("blah");
    }

    public override async Task DuplexStreaming(IAsyncStreamReader<RequestMessage> requestStream,
        IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
    {
        await responseStream.WriteAsync(new ResponseMessage { Value = 10 });
        await responseStream.WriteAsync(new ResponseMessage { Value = 11 });
        await Task.Delay(100);
        while (await requestStream.MoveNext())
        {
            await responseStream.WriteAsync(new ResponseMessage { Value = requestStream.Current.Value });
        }
    }

    public override async Task DelayedDuplexStreaming(IAsyncStreamReader<RequestMessage> requestStream,
        IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
    {
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            await responseStream.WriteAsync(new ResponseMessage { Value = requestStream.Current.Value });
            await Task.Delay(2000, context.CancellationToken);
        }
    }

    public override async Task ThrowingDuplexStreaming(IAsyncStreamReader<RequestMessage> requestStream,
        IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
    {
        while (await requestStream.MoveNext())
        {
            await responseStream.WriteAsync(new ResponseMessage { Value = requestStream.Current.Value });
        }
        throw new Exception("blah");
    }

    public override async Task<ResponseMessage> HeadersTrailers(RequestMessage request, ServerCallContext context)
    {
        RequestHeaders = context.RequestHeaders;
        await context.WriteResponseHeadersAsync(ResponseHeaders);
        foreach (var entry in ResponseTrailers)
        {
            if (entry.IsBinary)
            {
                context.ResponseTrailers.Add(entry.Key, entry.ValueBytes);
            }
            else
            {
                context.ResponseTrailers.Add(entry.Key, entry.Value);
            }
        }
        return new ResponseMessage
        {
            Value = request.Value,
            Binary = request.Binary
        };
    }

    public override Task<ResponseMessage> SetStatus(RequestMessage request, ServerCallContext context)
    {
        context.Status = new Status(StatusCode.InvalidArgument, "invalid argument");
        return Task.FromResult(new ResponseMessage());
    }

    public override Task<ResponseMessage> GetCallInfo(RequestMessage request, ServerCallContext context)
    {
        Peer = context.Peer;
        return Task.FromResult(new ResponseMessage());
    }

    public override Task<ResponseMessage> DropConnection(RequestMessage request, ServerCallContext context)
    {
        Thread.Sleep(100);
        if (context is NamedPipeCallContext namedPipeCallContext)
        {
            namedPipeCallContext.DisconnectPipeStream();
        }
        return Task.FromResult(new ResponseMessage());
    }

    public override Task<ResponseMessage> DropConnectionClientStreaming(IAsyncStreamReader<RequestMessage> requestStream,
        ServerCallContext context)
    {
        Thread.Sleep(100);
        if (context is NamedPipeCallContext namedPipeCallContext)
        {
            namedPipeCallContext.DisconnectPipeStream();
        }
        return Task.FromResult(new ResponseMessage());
    }

    public override async Task<ResponseMessage> WaitForCancellation(RequestMessage request, ServerCallContext context)
    {
        try
        {
            await Task.Delay(2000, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            CancellationOccurred = true;
            throw;
        }
        return new ResponseMessage();
    }

    public bool CancellationOccurred { get; private set; }

    public Metadata RequestHeaders { get; private set; }

    public Metadata ResponseHeaders { private get; set; }

    public Metadata ResponseTrailers { private get; set; }

    public string Peer { get; private set; }
}