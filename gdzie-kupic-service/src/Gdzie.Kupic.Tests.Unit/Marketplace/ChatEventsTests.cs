using Gdzie.Kupic.Chat;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Marketplace;

public class ChatEventsTests
{
    private sealed class ThrowingChat : IChatChannel
    {
        public Task MessageReceivedAsync(Guid userId, Guid threadId, Guid messageId, CancellationToken ct = default) => throw new InvalidOperationException("down");
        public Task ThreadUpdatedAsync(Guid userId, Guid threadId, CancellationToken ct = default) => throw new InvalidOperationException("down");
    }

    private sealed class ThrowingNotifications : INotificationChannel
    {
        public Task NotificationRaisedAsync(Guid userId, NotificationKind kind, Guid? postId, Guid? threadId, CancellationToken ct = default) =>
            throw new InvalidOperationException("down");
    }

    [Test]
    public async Task FailingChannels_AreSwallowed()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var merchantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var post = new Post(Guid.NewGuid(), Guid.NewGuid(), new Gdzie.Kupic.Domain.Model.Location.Coordinates(50, 19), 5m,
            Guid.NewGuid(), Guid.NewGuid(), "T", null, null, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow);
        db.Posts.Add(post);
        db.ChatThreads.Add(new Gdzie.Kupic.Domain.Model.Chat.ChatThread(threadId, post.Id, merchantId, false, DateTimeOffset.UtcNow));
        db.MerchantAccounts.Add(new MerchantAccount(Guid.NewGuid(), merchantId, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var events = new ChatEvents(new ChatStorage(db), new ThrowingChat(), new ThrowingNotifications(), NullLogger<ChatEvents>.Instance);

        await Should.NotThrowAsync(() => events.MessageReceivedAsync(threadId, Guid.NewGuid(), ChatSide.Buyer));
        await Should.NotThrowAsync(() => events.MessageReceivedAsync(threadId, Guid.NewGuid(), ChatSide.Merchant));
        await Should.NotThrowAsync(() => events.ThreadCreatedAsync(threadId));
        await Should.NotThrowAsync(() => events.ThreadsLockChangedAsync([threadId]));
    }
}
