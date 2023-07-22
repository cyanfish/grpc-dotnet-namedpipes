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

namespace GrpcDotNetNamedPipes.Internal.Helpers;

internal class ByteArrayBufferWriter : IBufferWriter<byte>
{
    private readonly byte[] _buffer;
    private int _position;

    public ByteArrayBufferWriter(int size)
    {
        _buffer = new byte[size];
    }

    public void Advance(int count)
    {
        _position += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0) => _buffer.AsMemory(_position);

    public Span<byte> GetSpan(int sizeHint = 0) => _buffer.AsSpan(_position);

    public byte[] Buffer => _buffer;
}