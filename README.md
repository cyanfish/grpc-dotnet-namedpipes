# GrpcDotNetNamedPipes

[![NuGet](https://img.shields.io/nuget/v/GrpcDotNetNamedPipes)](https://www.nuget.org/packages/GrpcDotNetNamedPipes/)

Named pipe transport for [gRPC](https://grpc.io/) in C#/.NET.

**This is not an official Google product.**

## Supported platforms

- .NET Framework 4.6.2+ (Windows)
- .NET 6+ (Windows, macOS, Linux)

## Usage

Suppose you have a Greeter service as described in
the [gRPC on .NET Core](https://docs.microsoft.com/en-us/aspnet/core/grpc/) intro.

Server:

```
var server = new NamedPipeServer("MY_PIPE_NAME");
Greeter.BindService(server.ServiceBinder, new GreeterService());
server.Start();
```

Client:

```
var channel = new NamedPipeChannel(".", "MY_PIPE_NAME");
var client = new Greeter.GreeterClient(channel);

var response = await client.SayHelloAsync(
	new HelloRequest { Name = "World" });

Console.WriteLine(response.Message);
```

## Why named pipes?

Named pipes are suitable for inter-process communication (IPC).

Since the introduction of this project, ASP.NET Core has added support for gRPC
over [Unix Domain Sockets](https://learn.microsoft.com/en-us/aspnet/core/grpc/interprocess-uds?view=aspnetcore-8.0) and
over [Named Pipes](https://learn.microsoft.com/en-us/aspnet/core/grpc/interprocess-namedpipes?view=aspnetcore-8.0). Here
is a handy matrix to help you decide what's right for you:

|                            | GrpcDotNetNamedPipes           | ASP.NET UDS                | ASP.NET Named Pipes                | ASP.NET HTTP              |
|----------------------------|--------------------------------|----------------------------|------------------------------------|---------------------------|
| .NET Platform              | .NET Framework 4.6.2<br>.NET 5 | .NET 5                     | .NET 8 (server)<br>.NET 5 (client) | .NET 5                    |
| OS                         | Windows 7<br>Mac<br>Linux      | Windows 10<br>Mac<br>Linux | Windows 7                          | Windows 7<br>Mac<br>Linux |
| No firewall warnings       | :heavy_check_mark:             | :heavy_check_mark:         | :heavy_check_mark:                 | :x:                       |
| No network adapter         | :heavy_check_mark:             | :heavy_check_mark:         | :heavy_check_mark:                 | :x:                       |
| Access controls            | :heavy_check_mark:             | :x:                        | :heavy_check_mark:                 | :x:                       |
| Binary size (trimmed)      | **\~ 300 KB**                  | ~ 7 MB                     | **\~ 7 MB**                        | ~ 7 MB                    |
| Startup time               | < 25ms                         | < 25ms                     | < 25ms                             | ~ 250ms                   |
| Large message throughput   | **\~ 500MB/s**                 | ~ 400MB/s                  | **\~ 100MB/s**                     | ~ 100MB/s                 |
| Streaming messages         | ~ 400k/s                       | ~ 500k/s                   | ~ 500k/s                           | ~ 400k/s                  |
| Method calls               | ~ 8000/s                       | ~ 4000/s                   | ~ 5000/s                           | ~ 2500/s                  |
| Compatible with gRPC-Go    | :x:                            | :heavy_check_mark:         | :heavy_check_mark:                 | :heavy_check_mark:        |
| Official Microsoft support | :x:                            | :heavy_check_mark:         | :heavy_check_mark:                 | :heavy_check_mark:        |

Performance numbers are based
on [tests](https://github.com/cyanfish/grpc-dotnet-namedpipes/blob/master/GrpcDotNetNamedPipes.PerfTests/GrpcPerformanceTests.cs)
running on Windows 11 with .NET 8.

## Caveats

This implementation currently uses a custom wire protocol so it won't be compatible with other gRPC named pipe
implementations.

Linux and macOS support is provided for universal compatibility but may not be as optimized as Windows.