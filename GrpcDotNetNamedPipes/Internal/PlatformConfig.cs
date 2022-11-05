using System.IO.Pipes;

namespace GrpcDotNetNamedPipes.Internal
{
    public static class PlatformConfig
    {
        public static PipeTransmissionMode TransmissionMode { get; } =
#if NET6_0_OR_GREATER
            OperatingSystem.IsWindows() ? PipeTransmissionMode.Message : PipeTransmissionMode.Byte;
#elif NETSTANDARD
            Environment.OSVersion.Platform == PlatformID.Win32NT ? PipeTransmissionMode.Message : PipeTransmissionMode.Byte;
#else
            PipeTransmissionMode.Message;
#endif

        public static bool SizePrefix { get; } = TransmissionMode == PipeTransmissionMode.Byte;
    }
}