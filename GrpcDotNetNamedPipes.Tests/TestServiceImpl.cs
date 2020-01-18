using System;
using System.Threading.Tasks;
using Grpc.Core;
using GrpcDotNetNamedPipes.Tests.Generated;

namespace GrpcDotNetNamedPipes.Tests
{
    public class TestServiceImpl : TestService.TestServiceBase
    {
        public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("Test exception");

        public bool SimplyUnaryCalled { get; private set; }

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

            return new ResponseMessage {Value = total};
        }

        public override async Task ServerStreaming(RequestMessage request,
            IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        {
            for (int i = request.Value; i > 0; i--)
            {
                await responseStream.WriteAsync(new ResponseMessage {Value = i});
            }
        }

        public override async Task DelayedServerStreaming(RequestMessage request,
            IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        {
            for (int i = request.Value; i > 0; i--)
            {
                await responseStream.WriteAsync(new ResponseMessage {Value = i});
                await Task.Delay(1000, context.CancellationToken);
                if (context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        public override async Task DuplexStreaming(IAsyncStreamReader<RequestMessage> requestStream,
            IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        {
            await responseStream.WriteAsync(new ResponseMessage {Value = 10});
            await responseStream.WriteAsync(new ResponseMessage {Value = 11});
            await Task.Delay(100);
            while (await requestStream.MoveNext())
            {
                await responseStream.WriteAsync(new ResponseMessage {Value = requestStream.Current.Value});
            }
        }

        public override async Task DelayedDuplexStreaming(IAsyncStreamReader<RequestMessage> requestStream,
            IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        {
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                await responseStream.WriteAsync(new ResponseMessage {Value = requestStream.Current.Value});
                await Task.Delay(1000, context.CancellationToken);
            }
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

        public Metadata RequestHeaders { get; private set; }
        
        public Metadata ResponseHeaders { private get; set; }
        
        public Metadata ResponseTrailers { private get; set; }
    }
}