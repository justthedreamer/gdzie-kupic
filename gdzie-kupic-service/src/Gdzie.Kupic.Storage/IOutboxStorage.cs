namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Infrastructure;

public interface IOutboxStorage
{
    /// <summary>
    /// Claims up to <paramref name="batchSize"/> unprocessed entries (concurrent callers never get the
    /// same entry), runs <paramref name="handler"/> for each and marks them processed in one transaction.
    /// If the process dies after the handler ran, the entry is handled again - handlers must be idempotent.
    /// </summary>
    Task<int> ProcessPendingAsync(
        int batchSize, DateTimeOffset now, Func<OutboxMessage, Task> handler, CancellationToken ct = default);
}
