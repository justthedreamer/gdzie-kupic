namespace Gdzie.Kupic.Storage;

using Gdzie.Kupic.Domain.Model.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal sealed class OutboxStorage(AppDbContext db) : IOutboxStorage
{
    public async Task<int> ProcessPendingAsync(
        int batchSize, DateTimeOffset now, Func<OutboxMessage, Task> handler, CancellationToken ct = default)
    {
        // Row locks and transactions are PostgreSQL-specific; other providers (tests) run single-threaded.
        if (!db.Database.IsNpgsql())
        {
            var pending = await db.Outbox
                .Where(o => o.ProcessedAt == null)
                .OrderBy(o => o.CreatedAt)
                .Take(batchSize)
                .ToListAsync(ct);

            return await HandleAsync(pending, now, handler, ct);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var claimed = await db.Outbox
            .FromSql($"""
                      SELECT * FROM "Outbox"
                      WHERE "ProcessedAt" IS NULL
                      ORDER BY "CreatedAt"
                      LIMIT {batchSize}
                      FOR UPDATE SKIP LOCKED
                      """)
            .ToListAsync(ct);

        var handled = await HandleAsync(claimed, now, handler, ct);
        await transaction.CommitAsync(ct);

        return handled;
    }

    private async Task<int> HandleAsync(
        List<OutboxMessage> messages, DateTimeOffset now, Func<OutboxMessage, Task> handler, CancellationToken ct)
    {
        foreach (var message in messages)
        {
            await handler(message);
            message.ProcessedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return messages.Count;
    }
}
