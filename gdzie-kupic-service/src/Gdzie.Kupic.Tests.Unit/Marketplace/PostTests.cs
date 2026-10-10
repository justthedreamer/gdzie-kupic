using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Marketplace;

public class PostTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static Post NewPost(DateTimeOffset? urgentDeadline = null, TimeSpan? lifetime = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), new Coordinates(50, 19), 5m, Guid.NewGuid(), Guid.NewGuid(), "Title", null,
            urgentDeadline, Post.CalculateExpiry(Now, urgentDeadline, lifetime ?? TimeSpan.FromHours(72)), Now);

    [Test]
    public void NewPost_IsActiveAndPending()
    {
        var post = NewPost();

        post.Status.ShouldBe(PostStatus.Active);
        post.NotificationDispatchStatus.ShouldBe(NotificationDispatchStatus.Pending);
        post.IsLongLived.ShouldBeFalse();
    }

    [Test]
    public void Expiry_NonUrgent_UsesDefaultLifetime() =>
        Post.CalculateExpiry(Now, null, TimeSpan.FromHours(72)).ShouldBe(Now.AddHours(72));

    [Test]
    public void Expiry_Urgent_UsesDeadline() =>
        Post.CalculateExpiry(Now, Now.AddHours(5), TimeSpan.FromHours(72)).ShouldBe(Now.AddHours(5));

    [Test]
    public void Fulfil_ActiveOpenPost_Succeeds()
    {
        var post = NewPost();

        post.TryFulfil(Now.AddHours(1)).ShouldBeTrue();

        post.Status.ShouldBe(PostStatus.Fulfilled);
    }

    [Test]
    public void Close_ActiveOpenPost_Succeeds()
    {
        var post = NewPost();

        post.TryClose(Now.AddHours(1)).ShouldBeTrue();

        post.Status.ShouldBe(PostStatus.Closed);
    }

    [Test]
    public void Transitions_PastExpiry_AreRejectedEvenBeforeExpiryJobRuns()
    {
        var post = NewPost();

        post.TryFulfil(post.ExpiresAt).ShouldBeFalse();
        post.TryClose(post.ExpiresAt.AddMinutes(1)).ShouldBeFalse();

        post.Status.ShouldBe(PostStatus.Active);
    }

    [Test]
    public void Transitions_StartFromActiveOnly()
    {
        var post = NewPost();
        post.TryClose(Now).ShouldBeTrue();

        post.TryFulfil(Now).ShouldBeFalse();
        post.TryClose(Now).ShouldBeFalse();
        post.TryExpire(post.ExpiresAt.AddDays(1)).ShouldBeFalse();

        post.Status.ShouldBe(PostStatus.Closed);
    }

    [Test]
    public void Expire_OverduePost_IsIdempotent()
    {
        var post = NewPost();

        post.TryExpire(post.ExpiresAt).ShouldBeTrue();
        post.TryExpire(post.ExpiresAt.AddHours(1)).ShouldBeFalse();

        post.Status.ShouldBe(PostStatus.Expired);
    }

    [Test]
    public void Expire_BeforeDeadline_DoesNothing()
    {
        var post = NewPost();

        post.TryExpire(post.ExpiresAt.AddSeconds(-1)).ShouldBeFalse();

        post.Status.ShouldBe(PostStatus.Active);
    }

    [Test]
    public void LongLived_EligiblePost_ExtendsExpiry()
    {
        var post = NewPost();
        post.MarkDispatched(Now);

        post.TryMakeLongLived(Now.AddHours(2), 0, TimeSpan.FromDays(14)).ShouldBeTrue();

        post.IsLongLived.ShouldBeTrue();
        post.ExpiresAt.ShouldBe(Now.AddHours(2).AddDays(14));
    }

    [Test]
    public void LongLived_NotEligible_WhenPending()
    {
        NewPost().TryMakeLongLived(Now, 0, TimeSpan.FromDays(14)).ShouldBeFalse();
    }

    [Test]
    public void LongLived_NotEligible_WhenUrgent()
    {
        var post = NewPost(Now.AddHours(5));
        post.MarkDispatched(Now);

        post.TryMakeLongLived(Now, 0, TimeSpan.FromDays(14)).ShouldBeFalse();
    }

    [Test]
    public void LongLived_NotEligible_WhenMerchantsWereNotified()
    {
        var post = NewPost();
        post.MarkDispatched(Now);

        post.TryMakeLongLived(Now, 1, TimeSpan.FromDays(14)).ShouldBeFalse();
    }

    [Test]
    public void LongLived_NotEligible_WhenAlreadyLongLivedOrNotActive()
    {
        var post = NewPost();
        post.MarkDispatched(Now);
        post.TryMakeLongLived(Now, 0, TimeSpan.FromDays(14)).ShouldBeTrue();
        post.TryMakeLongLived(Now, 0, TimeSpan.FromDays(14)).ShouldBeFalse();

        var closed = NewPost();
        closed.MarkDispatched(Now);
        closed.TryClose(Now);
        closed.TryMakeLongLived(Now, 0, TimeSpan.FromDays(14)).ShouldBeFalse();
    }
}
