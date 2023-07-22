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

using System.Security.Principal;

namespace GrpcDotNetNamedPipes;

public class NamedPipeChannelOptions
{
#if NET6_0_OR_GREATER
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

    /// <summary>
    /// Gets or sets a value indicating the number of milliseconds to wait for the server to respond before the connection times out.
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30 * 1000;
}