using System.Security.Principal;

namespace GrpcDotNetNamedPipes
{
    public class NamedPipeChannelOptions
    {
#if NETCOREAPP
        /// <summary>
        /// Gets or sets a value indicating whether the client pipe can only connect to a server created by the same
        /// user.
        /// </summary>
        public bool CurrentUserOnly { get; set; }
#endif

        /// <summary>
        /// Gets or sets a value indicating the security impersonation level.
        /// </summary>
        public TokenImpersonationLevel ImpersonationLevel { get; set; }
    }
}