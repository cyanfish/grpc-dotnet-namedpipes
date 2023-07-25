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

using System.Runtime.InteropServices;
using System.Text;

namespace GrpcDotNetNamedPipes.Internal.Helpers;

internal class PipeInterop
{
    private const int ErrorPipeLocal = 229;

    public static string GetClientComputerName(IntPtr pipeHandle)
    {
        var buffer = new StringBuilder(1024);
        if (!GetNamedPipeClientComputerName(pipeHandle, buffer, 1024))
        {
            if (Marshal.GetLastWin32Error() == ErrorPipeLocal)
            {
                return "localhost";
            }
            throw new InvalidOperationException($"Error retrieving client computer name: {Marshal.GetLastWin32Error()}");
        }
        return buffer.ToString();
    }

    public static uint GetClientProcessId(IntPtr pipeHandle)
    {
        if (!GetNamedPipeClientProcessId(pipeHandle, out var processId))
        {
            throw new InvalidOperationException($"Error retrieving client process id: {Marshal.GetLastWin32Error()}");
        }
        return processId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientComputerName(IntPtr pipeHandle, StringBuilder buffer, uint bufferLen);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(IntPtr pipeHandle, out uint processId);
}