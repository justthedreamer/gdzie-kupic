namespace Gdzie.Kupic.Marketplace;

using System.Text.Json;
using Gdzie.Kupic.Domain.Model.Infrastructure;

internal static class OutboxMessages
{
    public static OutboxMessage NotifyMerchants(Guid postId, DateTimeOffset now) =>
        new(Guid.NewGuid(), OutboxMessageTypes.NotifyMerchants, JsonSerializer.Serialize(new { postId }), now);

    public static OutboxMessage NotifyNewMerchant(Guid merchantId, DateTimeOffset now) =>
        new(Guid.NewGuid(), OutboxMessageTypes.NotifyNewMerchant, JsonSerializer.Serialize(new { merchantId }), now);
}
