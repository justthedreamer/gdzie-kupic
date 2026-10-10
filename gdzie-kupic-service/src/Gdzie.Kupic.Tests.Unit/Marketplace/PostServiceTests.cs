using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Marketplace;

public class PostServiceTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private AppDbContext _db = null!;
    private FixedTimeProvider _clock = null!;
    private PostService _service = null!;
    private Guid _buyerId;
    private Category _category = null!;
    private Tag _tag = null!;

    [SetUp]
    public async Task SetUp()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        _service = new PostService(
            new PostStorage(_db), new ResponseStorage(_db), new CatalogueStorage(_db), Options.Create(new MarketplaceSettings()),
            new PostFeedEvents(new PostStorage(_db), new NullPostFeedChannel(), NullLogger<PostFeedEvents>.Instance), _clock);

        _buyerId = Guid.NewGuid();
        _category = new Category(Guid.NewGuid(), "Audio", false, _clock.Now);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, _clock.Now);
        _db.Categories.Add(_category);
        _db.Tags.Add(_tag);
        await _db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private CreatePostInput Input(
        decimal? radius = 5m, Guid? categoryId = null, Guid? tagId = null, string? title = "Need a mic",
        string? description = null, DateTimeOffset? deadline = null) =>
        new(50.06, 19.94, radius, categoryId ?? _category.Id, tagId ?? _tag.Id, title, description, deadline);

    [Test]
    public async Task Create_NonUrgent_ExpiresAfterDefaultLifetime()
    {
        var result = await _service.CreateAsync(_buyerId, Input());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Post.ExpiresAt.ShouldBe(_clock.Now.AddHours(72));
        result.Value.Post.IsUrgent.ShouldBeFalse();
        result.Value.Post.Status.ShouldBe(PostStatus.Active);
        result.Value.Post.NotificationDispatchStatus.ShouldBe(NotificationDispatchStatus.Pending);
    }

    [Test]
    public async Task Create_Urgent_ExpiresAtDeadline()
    {
        var deadline = _clock.Now.AddHours(10);

        var result = await _service.CreateAsync(_buyerId, Input(deadline: deadline));

        result.Value!.Post.ExpiresAt.ShouldBe(deadline);
        result.Value.Post.IsUrgent.ShouldBeTrue();
    }

    [Test]
    public async Task Create_UnlimitedRadius_StoresNull()
    {
        var result = await _service.CreateAsync(_buyerId, Input(radius: null));

        result.Value!.Post.RadiusKm.ShouldBeNull();
    }

    [Test]
    public async Task Create_WritesPostAndOutboxEntry()
    {
        var result = await _service.CreateAsync(_buyerId, Input());

        var outbox = await _db.Outbox.SingleAsync();
        outbox.Type.ShouldBe("NotifyMerchants");
        outbox.Payload.ShouldContain(result.Value!.Post.Id.ToString());
        outbox.ProcessedAt.ShouldBeNull();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Create_NonPositiveRadius_Fails(int radius) =>
        await AssertValidationFailure(Input(radius: radius));

    [TestCase(30)]
    [TestCase(73 * 60)]
    public async Task Create_UrgentDeadlineOutsideWindow_Fails(int minutesAhead) =>
        await AssertValidationFailure(Input(deadline: _clock.Now.AddMinutes(minutesAhead)));

    [TestCase(60)]
    [TestCase(72 * 60)]
    public async Task Create_UrgentDeadlineOnWindowEdges_Succeeds(int minutesAhead)
    {
        var result = await _service.CreateAsync(_buyerId, Input(deadline: _clock.Now.AddMinutes(minutesAhead)));

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task Create_TagFromAnotherCategory_Fails()
    {
        var other = new Category(Guid.NewGuid(), "Video", false, _clock.Now);
        _db.Categories.Add(other);
        await _db.SaveChangesAsync();

        await AssertValidationFailure(Input(categoryId: other.Id));
    }

    [Test]
    public async Task Create_DisabledCategoryOrTag_Fails()
    {
        _tag.IsDisabled = true;
        await _db.SaveChangesAsync();
        await AssertValidationFailure(Input());

        _tag.IsDisabled = false;
        _category.IsDisabled = true;
        await _db.SaveChangesAsync();
        await AssertValidationFailure(Input());
    }

    [Test]
    public async Task Create_OverLongOrBlankText_Fails()
    {
        await AssertValidationFailure(Input(title: new string('a', PostService.MaxTitleLength + 1)));
        await AssertValidationFailure(Input(title: "  "));
        await AssertValidationFailure(Input(description: new string('a', PostService.MaxDescriptionLength + 1)));
    }

    [Test]
    public async Task Fulfil_OtherBuyersPost_ReturnsNotFound()
    {
        var post = (await _service.CreateAsync(_buyerId, Input())).Value!.Post;

        var result = await _service.FulfilAsync(Guid.NewGuid(), post.Id);

        result.Error.ShouldBe(PostError.NotFound);
    }

    [Test]
    public async Task Fulfil_PastExpiry_ConflictEvenIfStillActive()
    {
        var post = (await _service.CreateAsync(_buyerId, Input())).Value!.Post;
        _clock.Now = post.ExpiresAt.AddMinutes(1);

        var result = await _service.FulfilAsync(_buyerId, post.Id);

        result.Error.ShouldBe(PostError.Conflict);
    }

    [Test]
    public async Task Close_ThenFulfil_Conflicts()
    {
        var post = (await _service.CreateAsync(_buyerId, Input())).Value!.Post;

        (await _service.CloseAsync(_buyerId, post.Id)).IsSuccess.ShouldBeTrue();
        (await _service.FulfilAsync(_buyerId, post.Id)).Error.ShouldBe(PostError.Conflict);
    }

    [Test]
    public async Task List_FiltersByScopeAndOwnerNewestFirst()
    {
        var first = (await _service.CreateAsync(_buyerId, Input(title: "first"))).Value!.Post;
        _clock.Now = _clock.Now.AddMinutes(1);
        var second = (await _service.CreateAsync(_buyerId, Input(title: "second"))).Value!.Post;
        await _service.CreateAsync(Guid.NewGuid(), Input(title: "someone else"));
        await _service.CloseAsync(_buyerId, first.Id);

        var active = await _service.ListAsync(_buyerId, PostScope.Active);
        var ended = await _service.ListAsync(_buyerId, PostScope.Ended);

        active.Value!.Select(v => v.Post.Id).ShouldBe([second.Id]);
        ended.Value!.Select(v => v.Post.Id).ShouldBe([first.Id]);
    }

    [Test]
    public async Task ExpireJob_MovesOverduePostsAndIsIdempotent()
    {
        var post = (await _service.CreateAsync(_buyerId, Input())).Value!.Post;
        var storage = new PostStorage(_db);
        _db.ChangeTracker.Clear();

        (await storage.ExpireOverduePostsAsync(_clock.Now)).ShouldBeEmpty();
        (await storage.ExpireOverduePostsAsync(post.ExpiresAt)).ShouldBe([post.Id]);
        (await storage.ExpireOverduePostsAsync(post.ExpiresAt)).ShouldBeEmpty();

        (await _db.Posts.SingleAsync()).Status.ShouldBe(PostStatus.Expired);
    }

    private async Task AssertValidationFailure(CreatePostInput input)
    {
        var result = await _service.CreateAsync(_buyerId, input);

        result.Error.ShouldBe(PostError.Validation);
        (await _db.Posts.CountAsync()).ShouldBe(0);
        (await _db.Outbox.CountAsync()).ShouldBe(0);
    }
}
