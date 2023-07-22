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

internal class ByteArraySerializationContext : SerializationContext
{
    private int _payloadLength;
    private ByteArrayBufferWriter _bufferWriter;

    public override void Complete(byte[] payload)
    {
        SerializedData = payload;
    }

    public override IBufferWriter<byte> GetBufferWriter()
    {
        return _bufferWriter ??= new ByteArrayBufferWriter(_payloadLength);
    }

    public override void SetPayloadLength(int payloadLength)
    {
        _payloadLength = payloadLength;
    }

    public override void Complete()
    {
        Debug.Assert(_bufferWriter.Buffer.Length == _payloadLength);
        SerializedData = _bufferWriter.Buffer;
    }

    public byte[] SerializedData { get; private set; }
}