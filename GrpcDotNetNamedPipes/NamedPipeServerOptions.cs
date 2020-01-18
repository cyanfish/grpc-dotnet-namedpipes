using System.IO.Pipes;

namespace GrpcDotNetNamedPipes
{
    public class NamedPipeServerOptions
    {
#if NETCOREAPP
        /// <summary>
        /// Gets or sets a value indicating whether the server pipe can only be connected to a client created by the
        /// same user.
        /// </summary>
        public bool CurrentUserOnly { get; set; }
#endif
#if NETFRAMEWORK
        /// <summary>
        /// Gets or sets a value indicating the access control to be used for the pipe.
        /// </summary>
        public PipeSecurity PipeSecurity { get; set; }
#endif
    }
}