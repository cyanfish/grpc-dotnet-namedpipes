# GrpcDotNetNamedPipes

[![NuGet](https://img.shields.io/nuget/v/GrpcDotNetNamedPipes)](https://www.nuget.org/packages/GrpcDotNetNamedPipes/)

Named pipe transport for [gRPC](https://grpc.io/) in C#/.NET.

**This is not an official Google product.**

## Supported platforms

- .NET Framework 4.6.2+ (Windows)
- .NET 6+ (Windows, macOS, Linux)

## Usage

Suppose you have a Greeter service as described in the [gRPC on .NET Core](https://docs.microsoft.com/en-us/aspnet/core/grpc/) intro.

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

Compared with gRPC over HTTP (using [grpc](https://github.com/grpc/grpc) or [grpc-dotnet](https://github.com/grpc/grpc-dotnet)), you get:
- Better access controls (e.g. current user only)
- Lightweight pure .NET library (instead of 3MB+ native DLL or ASP.NET Core dependency)
- Much faster startup time
- 2x-3x faster large message throughput
- No firewall warnings
- No network adapter required

## Caveats

This implementation currently uses a custom wire protocol so it won't be compatible with other gRPC named pipe implementations.

Linux and macOS support is provided for universal compatibility but may not be as optimized as Windows.