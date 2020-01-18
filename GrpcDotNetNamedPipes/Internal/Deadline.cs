using System;
using System.Threading;

namespace GrpcDotNetNamedPipes.Internal
{
    public class Deadline
    {
        private readonly DateTime? _deadline;
        private readonly CancellationTokenSource _cts;

        public Deadline(DateTime? deadline)
        {
            _deadline = deadline;
            _cts = new CancellationTokenSource();
            
            if (_deadline != null)
            {
                long millis = (long) (_deadline.Value - DateTime.UtcNow).TotalMilliseconds;
                if (millis <= 0)
                {
                    _cts.Cancel();
                }
                else if (millis < int.MaxValue)
                {
                    _cts.CancelAfter((int) millis);
                }
            }
        }

        public DateTime Value => _deadline ?? DateTime.MaxValue;

        public CancellationToken Token => _cts.Token;

        public bool IsExpired => _cts.IsCancellationRequested;
    }
}