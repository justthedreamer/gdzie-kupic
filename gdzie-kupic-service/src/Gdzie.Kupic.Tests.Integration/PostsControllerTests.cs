using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Posts;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class PostsControllerTests : IntegrationTestBase
{
    private const string Url = "/api/posts";

    private Category _category = null!;
    private Tag _tag = null!;

    [SetUp]
    public async Task SeedCatalogue()
    {
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);

        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Categories.Add(_category);
        db.Tags.Add(_tag);
        await db.SaveChangesAsync();
    }

    private Posts.CreateRequest Request(
        decimal? radius = 5m, DateTimeOffset? deadline = null, string title = "Need a microphone") =>
        new(50.06, 19.94, radius, _category.Id, _tag.Id, title, null, deadline);

    private async Task<Posts.PostWithCountDto> CreatePostAsync(Posts.CreateRequest? request = null)
    {
        var response = await Client.PostAsJsonAsync(Url, request ?? Request());
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await ReadAsAsync<Posts.PostWithCountDto>(response))!;
    }

    [Test]
    public async Task Anonymous_ReturnsUnauthorized()
    {
        (await Client.GetAsync($"{Url}?scope=active")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync(Url, Request())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(Role.Merchant)]
    [TestCase(Role.Admin)]
    public async Task NonBuyer_ReturnsForbidden(Role role)
    {
        await AuthenticateAsync(role);

        (await Client.GetAsync($"{Url}?scope=active")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PostAsJsonAsync(Url, Request())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PostAsync($"{Url}/{Guid.NewGuid()}/close", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Create_ReturnsActivePendingPost()
    {
        await AuthenticateAsync(Role.Buyer);

        var post = await CreatePostAsync();

        post.Status.ShouldBe("Active");
        post.NotificationDispatchStatus.ShouldBe("Pending");
        post.RadiusKm.ShouldBe(5m);
        post.Category.Name.ShouldBe("Audio");
        post.Tag.Name.ShouldBe("Microphones");
        post.IsUrgent.ShouldBeFalse();
        post.IsLongLived.ShouldBeFalse();
        post.NotifiedCount.ShouldBe(0);
        (post.ExpiresAt - post.CreatedAt).ShouldBe(TimeSpan.FromHours(72), TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_WritesOutboxEntryInSameTransaction()
    {
        await AuthenticateAsync(Role.Buyer);

        var post = await CreatePostAsync();

        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await db.Outbox.SingleAsync();
        outbox.Type.ShouldBe("NotifyMerchants");
        outbox.Payload.ShouldContain(post.Id.ToString());
        outbox.ProcessedAt.ShouldBeNull();
    }

    [Test]
    public async Task Create_UnlimitedRadius_ReturnsNullRadius()
    {
        await AuthenticateAsync(Role.Buyer);

        var post = await CreatePostAsync(Request(radius: null));

        post.RadiusKm.ShouldBeNull();
    }

    [Test]
    public async Task Create_Urgent_ExpiresAtDeadline()
    {
        await AuthenticateAsync(Role.Buyer);
        var deadline = DateTimeOffset.UtcNow.AddHours(6);

        var post = await CreatePostAsync(Request(deadline: deadline));

        post.IsUrgent.ShouldBeTrue();
        post.ExpiresAt.ShouldBe(deadline, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_InvalidInput_ReturnsBadRequestAndStoresNothing()
    {
        await AuthenticateAsync(Role.Buyer);

        var bad = new[]
        {
            Request(radius: 0),
            Request(deadline: DateTimeOffset.UtcNow.AddMinutes(10)),
            Request(deadline: DateTimeOffset.UtcNow.AddHours(80)),
            Request(title: new string('x', 201)),
            Request() with { TagId = Guid.NewGuid() },
        };

        foreach (var request in bad)
            (await Client.PostAsJsonAsync(Url, request)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Posts.CountAsync()).ShouldBe(0);
        (await db.Outbox.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Create_DisabledTag_ReturnsBadRequest()
    {
        await AuthenticateAsync(Role.Buyer);
        using (var scope = IntegrationTestSetup.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Tags.SingleAsync()).IsDisabled = true;
            await db.SaveChangesAsync();
        }

        (await Client.PostAsJsonAsync(Url, Request())).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task FulfilAndClose_ActivePost_ReturnNoContentThenConflict()
    {
        await AuthenticateAsync(Role.Buyer);
        var first = await CreatePostAsync();
        var second = await CreatePostAsync();

        (await Client.PostAsync($"{Url}/{first.Id}/fulfil", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.PostAsync($"{Url}/{first.Id}/close", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Client.PostAsync($"{Url}/{second.Id}/close", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.PostAsync($"{Url}/{second.Id}/fulfil", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await Client.GetFromJsonAsync<Posts.PostWithCountDto>($"{Url}/{first.Id}"))!.Status.ShouldBe("Fulfilled");
        (await Client.GetFromJsonAsync<Posts.PostWithCountDto>($"{Url}/{second.Id}"))!.Status.ShouldBe("Closed");
    }

    [Test]
    public async Task Fulfil_PostPastExpiry_ReturnsConflictBeforeExpiryJobRuns()
    {
        await AuthenticateAsync(Role.Buyer);
        var post = await CreatePostAsync();
        await SetExpiresAtAsync(post.Id, DateTimeOffset.UtcNow.AddMinutes(-1));

        (await Client.PostAsync($"{Url}/{post.Id}/fulfil", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Client.PostAsync($"{Url}/{post.Id}/close", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task OtherBuyersPost_ReturnsNotFound()
    {
        await AuthenticateAsync(Role.Buyer);
        var post = await CreatePostAsync();

        await AuthenticateAsync(Role.Buyer);

        (await Client.GetAsync($"{Url}/{post.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.PostAsync($"{Url}/{post.Id}/fulfil", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.PostAsync($"{Url}/{post.Id}/close", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetFromJsonAsync<List<Posts.PostWithCountDto>>($"{Url}?scope=active"))!.ShouldBeEmpty();
    }

    [Test]
    public async Task List_FiltersByScopeNewestFirst()
    {
        await AuthenticateAsync(Role.Buyer);
        var older = await CreatePostAsync(Request(title: "older"));
        await Task.Delay(20);
        var newer = await CreatePostAsync(Request(title: "newer"));
        var ended = await CreatePostAsync(Request(title: "ended"));
        await Client.PostAsync($"{Url}/{ended.Id}/close", null);

        var active = (await Client.GetFromJsonAsync<List<Posts.PostWithCountDto>>($"{Url}?scope=active"))!;
        var endedList = (await Client.GetFromJsonAsync<List<Posts.PostWithCountDto>>($"{Url}?scope=ended"))!;

        active.Select(p => p.Id).ShouldBe([newer.Id, older.Id]);
        endedList.Select(p => p.Id).ShouldBe([ended.Id]);
    }

    [Test]
    public async Task List_InvalidScope_ReturnsBadRequest()
    {
        await AuthenticateAsync(Role.Buyer);

        (await Client.GetAsync($"{Url}?scope=nope")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ExpirePostsJob_ExpiresOnlyOverduePosts_AndIsIdempotent()
    {
        await AuthenticateAsync(Role.Buyer);
        var overdue = await CreatePostAsync();
        var fresh = await CreatePostAsync();
        await SetExpiresAtAsync(overdue.Id, DateTimeOffset.UtcNow.AddMinutes(-1));

        await RunExpireJobAsync();
        await RunExpireJobAsync();

        (await Client.GetFromJsonAsync<Posts.PostWithCountDto>($"{Url}/{overdue.Id}"))!.Status.ShouldBe("Expired");
        (await Client.GetFromJsonAsync<Posts.PostWithCountDto>($"{Url}/{fresh.Id}"))!.Status.ShouldBe("Active");
        (await Client.GetFromJsonAsync<List<Posts.PostWithCountDto>>($"{Url}?scope=ended"))!
            .Select(p => p.Id).ShouldBe([overdue.Id]);
    }

    private static async Task SetExpiresAtAsync(Guid postId, DateTimeOffset expiresAt)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var post = await db.Posts.SingleAsync(p => p.Id == postId);
        db.Entry(post).Property(p => p.ExpiresAt).CurrentValue = expiresAt;
        await db.SaveChangesAsync();
    }

    private static async Task RunExpireJobAsync()
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ExpirePostsJob>().RunAsync();
    }
}
