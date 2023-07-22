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

public class NamedPipeServerOptions
{
#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets or sets a value indicating whether the server pipe can only be connected to a client created by the
        /// same user.
        /// </summary>
        public bool CurrentUserOnly { get; set; }
#endif
#if NETFRAMEWORK || NET6_0_OR_GREATER
    /// <summary>
    /// Gets or sets a value indicating the access control to be used for the pipe.
    /// </summary>
    public PipeSecurity PipeSecurity { get; set; }
#endif
}