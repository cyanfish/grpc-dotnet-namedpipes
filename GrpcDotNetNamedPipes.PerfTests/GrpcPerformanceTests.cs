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

using Google.Protobuf;

namespace GrpcDotNetNamedPipes.PerfTests;

public class GrpcPerformanceTests
{
    private const int Timeout = 60_000;

    private readonly ITestOutputHelper _testOutputHelper;

    public GrpcPerformanceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory(Timeout = Timeout)]
    [ClassData(typeof(MultiChannelWithAspNetClassData))]
    public async Task ServerStreamingManyMessagesPerformance(ChannelContextFactory factory)
    {
        using var ctx = factory.Create();
        var stopwatch = Stopwatch.StartNew();
        var call = ctx.Client.ServerStreaming(new RequestMessage { Value = 100_000 });
        while (await call.ResponseStream.MoveNext())
        {
        }

        stopwatch.Stop();
        _testOutputHelper.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
    }

    [Theory(Timeout = Timeout)]
    [ClassData(typeof(MultiChannelWithAspNetClassData))]
    public void UnarySequentialChannelsPerformance(ChannelContextFactory factory)
    {
        using var ctx = factory.Create();
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1_000; i++)
        {
            var client = factory.CreateClient();
            client.SimpleUnary(new RequestMessage());
        }

        stopwatch.Stop();
        _testOutputHelper.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
    }

    // Windows seems to stall when 32+ threads try to connect to a named pipe at once
    [Theory(Timeout = Timeout, Skip = "named pipes fail with too many parallel channels")]
    [ClassData(typeof(MultiChannelWithAspNetClassData))]
    public async Task UnaryParallelChannelsPerformance(ChannelContextFactory factory)
    {
        using var ctx = factory.Create();
        var stopwatch = Stopwatch.StartNew();
        var tasks = new Task[1_000];
        for (int i = 0; i < tasks.Length; i++)
        {
            var client = factory.CreateClient();
            tasks[i] = client.SimpleUnaryAsync(new RequestMessage()).ResponseAsync;
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();
        _testOutputHelper.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
    }

    [Theory(Timeout = Timeout)]
    [ClassData(typeof(MultiChannelWithAspNetClassData))]
    public void UnarySequentialCallsPerformance(ChannelContextFactory factory)
    {
        using var ctx = factory.Create();
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1_000; i++)
        {
            ctx.Client.SimpleUnary(new RequestMessage());
        }

        stopwatch.Stop();
        _testOutputHelper.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
    }

    [Theory(Timeout = Timeout)]
    [ClassData(typeof(MultiChannelWithAspNetClassData))]
    public async Task UnaryParallelCallsPerformance(ChannelContextFactory factory)
    {
        using var ctx = factory.Create();
        var stopwatch = Stopwatch.StartNew();
        var tasks = new Task[1_000];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = ctx.Client.SimpleUnaryAsync(new RequestMessage()).ResponseAsync;
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();
        _testOutputHelper.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
    }

    [Theory(Timeout = Timeout)]
    [ClassData(typeof(MultiChannelWithAspNetClassData))]
    public void UnaryLargePayloadPerformance(ChannelContextFactory factory)
    {
        using var ctx = factory.Create();
        var bytes = new byte[100 * 1024 * 1024];
        var byteString = ByteString.CopyFrom(bytes);
        var stopwatch = Stopwatch.StartNew();
        ctx.Client.SimpleUnary(new RequestMessage { Binary = byteString });
        stopwatch.Stop();
        _testOutputHelper.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
    }

    [Theory(Timeout = Timeout)]
    [ClassData(typeof(MultiChannelWithAspNetClassData))]
    public void ChannelColdStartPerformance(ChannelContextFactory factory)
    {
        // Note: This test needs to be run on its own for accurate cold start measurements.
        var stopwatch = Stopwatch.StartNew();
        using var ctx = factory.Create();
        ctx.Client.SimpleUnary(new RequestMessage());
        stopwatch.Stop();
        _testOutputHelper.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
    }

    [Theory(Timeout = Timeout)]
    [ClassData(typeof(MultiChannelWithAspNetClassData))]
    public void ChannelWarmStartPerformance(ChannelContextFactory factory)
    {
        using var tempChannel = factory.Create();
        var stopwatch = Stopwatch.StartNew();
        using var ctx = factory.Create();
        ctx.Client.SimpleUnary(new RequestMessage());
        stopwatch.Stop();
        _testOutputHelper.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
    }
}