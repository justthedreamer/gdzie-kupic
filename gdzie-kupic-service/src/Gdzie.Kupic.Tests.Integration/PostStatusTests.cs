using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Service.API.Contract.Posts;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class PostStatusTests : IntegrationTestBase
{
    private Category _category = null!;
    private Tag _tag = null!;

    [SetUp]
    public async Task Seed()
    {
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);

        await WithDbAsync(async db =>
        {
            db.Categories.Add(_category);
            db.Tags.Add(_tag);
            await db.SaveChangesAsync();
        });

        await AuthenticateAsync(Role.Buyer);
    }

    private async Task<Guid> CreatePostAsync(DateTimeOffset? deadline = null)
    {
        var request = new Posts.CreateRequest(50.06, 19.94, 5m, _category.Id, _tag.Id, "Need a microphone", null, deadline);
        var response = await Client.PostAsJsonAsync("/api/posts", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await ReadAsAsync<Posts.PostWithCountDto>(response))!.Id;
    }

    private Task DispatchAsync(Guid postId, int notified = 0) => WithDbAsync(async db =>
    {
        var post = await db.Posts.SingleAsync(p => p.Id == postId);
        post.MarkDispatched(DateTimeOffset.UtcNow);
        for (var i = 0; i < notified; i++)
            db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), postId, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    });

    private async Task<Posts.StatusDto> GetStatusAsync(Guid id)
    {
        var response = await Client.GetAsync($"/api/posts/{id}/status");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsAsync<Posts.StatusDto>(response))!;
    }

    [Test]
    public async Task Status_Pending_IsNotZeroMatch()
    {
        var status = await GetStatusAsync(await CreatePostAsync());

        status.NotificationDispatchStatus.ShouldBe("Pending");
        status.NotifiedCount.ShouldBe(0);
        status.IsZeroMatch.ShouldBeFalse();
    }

    [Test]
    public async Task Status_DispatchedWithoutMatches_IsZeroMatch()
    {
        var id = await CreatePostAsync();
        await DispatchAsync(id);

        var status = await GetStatusAsync(id);

        status.NotificationDispatchStatus.ShouldBe("Dispatched");
        status.IsZeroMatch.ShouldBeTrue();
        status.CheckingCount.ShouldBe(0);
        status.HaveItCount.ShouldBe(0);
        status.MayHaveItCount.ShouldBe(0);
        status.CanOrderItCount.ShouldBe(0);
        status.CannotHelpCount.ShouldBe(0);
    }

    [Test]
    public async Task Status_DispatchedWithMatches_ReportsCountAndIsNotZeroMatch()
    {
        var id = await CreatePostAsync();
        await DispatchAsync(id, notified: 3);

        var status = await GetStatusAsync(id);

        status.NotifiedCount.ShouldBe(3);
        status.IsZeroMatch.ShouldBeFalse();
    }

    [Test]
    public async Task Status_UnknownOrForeignPost_ReturnsNotFound()
    {
        var id = await CreatePostAsync();

        (await Client.GetAsync($"/api/posts/{Guid.NewGuid()}/status")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await AuthenticateAsync(Role.Buyer);
        (await Client.GetAsync($"/api/posts/{id}/status")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Status_NonBuyer_ReturnsForbidden()
    {
        await AuthenticateAsync(Role.Merchant);

        (await Client.GetAsync($"/api/posts/{Guid.NewGuid()}/status")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PostAsync($"/api/posts/{Guid.NewGuid()}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task LongLived_ZeroMatchPost_ExtendsExpiry()
    {
        var id = await CreatePostAsync();
        await DispatchAsync(id);

        var response = await Client.PostAsync($"/api/posts/{id}/long-lived", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var post = (await ReadAsAsync<Posts.PostWithCountDto>(response))!;
        post.IsLongLived.ShouldBeTrue();
        post.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(13));
        post.ExpiresAt.ShouldBeLessThan(DateTimeOffset.UtcNow.AddDays(15));

        var stored = (await ReadAsAsync<Posts.PostWithCountDto>(await Client.GetAsync($"/api/posts/{id}")))!;
        stored.IsLongLived.ShouldBeTrue();
    }

    [Test]
    public async Task LongLived_BeforeDispatch_ReturnsConflict()
    {
        var id = await CreatePostAsync();

        (await Client.PostAsync($"/api/posts/{id}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task LongLived_WithMatches_ReturnsConflict()
    {
        var id = await CreatePostAsync();
        await DispatchAsync(id, notified: 1);

        (await Client.PostAsync($"/api/posts/{id}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task LongLived_UrgentPost_ReturnsConflict()
    {
        var id = await CreatePostAsync(DateTimeOffset.UtcNow.AddHours(5));
        await DispatchAsync(id);

        (await Client.PostAsync($"/api/posts/{id}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task LongLived_Twice_ReturnsConflict()
    {
        var id = await CreatePostAsync();
        await DispatchAsync(id);

        (await Client.PostAsync($"/api/posts/{id}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Client.PostAsync($"/api/posts/{id}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task LongLived_ClosedPost_ReturnsConflict()
    {
        var id = await CreatePostAsync();
        await DispatchAsync(id);
        (await Client.PostAsync($"/api/posts/{id}/close", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Client.PostAsync($"/api/posts/{id}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task LongLived_UnknownOrForeignPost_ReturnsNotFound()
    {
        var id = await CreatePostAsync();
        await DispatchAsync(id);

        (await Client.PostAsync($"/api/posts/{Guid.NewGuid()}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await AuthenticateAsync(Role.Buyer);
        (await Client.PostAsync($"/api/posts/{id}/long-lived", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

