using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Marketplace;

public class PostFeedEventsTests
{
    private sealed class ThrowingChannel : IPostFeedChannel
    {
        public Task PostAddedAsync(Guid userId, Guid postId, CancellationToken ct = default) => throw new InvalidOperationException("down");
        public Task PostRemovedAsync(Guid userId, Guid postId, CancellationToken ct = default) => throw new InvalidOperationException("down");
        public Task PostStatusChangedAsync(Guid userId, Guid postId, CancellationToken ct = default) => throw new InvalidOperationException("down");
    }

    [Test]
    public async Task FailingChannel_IsSwallowed()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var merchantId = Guid.NewGuid();
        db.MerchantAccounts.Add(new Gdzie.Kupic.Domain.Model.Marketplace.MerchantAccount(
            Guid.NewGuid(), merchantId, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var events = new PostFeedEvents(new PostStorage(db), new ThrowingChannel(), NullLogger<PostFeedEvents>.Instance);

        await Should.NotThrowAsync(() => events.PostAddedAsync(merchantId, Guid.NewGuid()));
        await Should.NotThrowAsync(() => events.PostRemovedAsync(Guid.NewGuid()));
        await Should.NotThrowAsync(() => events.PostStatusChangedAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
