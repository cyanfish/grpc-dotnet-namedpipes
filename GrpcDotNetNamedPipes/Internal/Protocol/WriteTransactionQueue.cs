namespace GrpcDotNetNamedPipes.Internal.Protocol;

internal class WriteTransactionQueue
{
    private readonly PipeStream _pipeStream;
    private List<WriteTransaction> _queue = new();
    private Task _dequeueTask;

    public WriteTransactionQueue(PipeStream pipeStream)
    {
        _pipeStream = pipeStream;
    }

    public void Add(WriteTransaction tx)
    {
        lock (this)
        {
            _queue.Add(tx);
            _dequeueTask ??= Task.Run(Dequeue);
        }
    }

    private void Dequeue()
    {
        while (true)
        {
            List<WriteTransaction> transactionsToWrite;
            lock (this)
            {
                transactionsToWrite = _queue;
                _queue = new List<WriteTransaction>();
            }
            // Merge transactions together if multiple are queued
            var mergedTx = new WriteTransaction(this);
            foreach (var tx in transactionsToWrite)
            {
                mergedTx.MergeFrom(tx);
            }
            mergedTx.WriteTo(_pipeStream);
            lock (this)
            {
                if (_queue.Count == 0)
                {
                    _dequeueTask = null;
                    break;
                }
            }
        }
    }
}