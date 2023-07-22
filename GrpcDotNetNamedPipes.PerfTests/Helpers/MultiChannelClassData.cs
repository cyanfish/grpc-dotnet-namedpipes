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

using System.Collections;
using System.Runtime.InteropServices;

namespace GrpcDotNetNamedPipes.PerfTests.Helpers;

public class MultiChannelWithAspNetClassData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[] { new NamedPipeChannelContextFactory() };
#if NET6_0_OR_GREATER
        if (RuntimeInformation.OSArchitecture == Architecture.Arm64 && !OperatingSystem.IsLinux())
        {
            // No grpc implementation available for comparison
            yield break;
        }
        yield return new object[] { new AspNetHttpContextFactory() };
        yield return new object[] { new AspNetUdsContextFactory() };
#endif
#if NET8_0_OR_GREATER
        yield return new object[] { new AspNetPipeContextFactory() };
#endif
        // The legacy HTTP gRPC is obsolete and much slower than anything else, so not much point comparing
        // yield return new object[] { new HttpChannelContextFactory() };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}