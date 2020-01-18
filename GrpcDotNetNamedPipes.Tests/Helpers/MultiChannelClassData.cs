using System.Collections;
using System.Collections.Generic;

namespace GrpcDotNetNamedPipes.Tests.Helpers
{
    public class MultiChannelClassData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] {new HttpChannelContextFactory()};
            yield return new object[] {new NamedPipeChannelContextFactory()};
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}