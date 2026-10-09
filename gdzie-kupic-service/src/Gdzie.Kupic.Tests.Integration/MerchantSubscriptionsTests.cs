using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class MerchantSubscriptionsTests : IntegrationTestBase
{
    private const string Url = "/api/merchant/subscriptions";

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

    private async Task OnboardMerchantAsync()
    {
        await AuthenticateAsync(Role.Merchant);
        var response = await Client.PostAsJsonAsync("/api/merchant/onboarding",
            new Onboarding.Request("Shop", null, new Onboarding.BranchRequest("Main", null, null, 50.06, 19.94, null)));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private Task<HttpResponseMessage> Subscribe(Guid categoryId, Guid? tagId = null) =>
        Client.PostAsJsonAsync(Url, new Subscriptions.Request(categoryId, tagId));

    [Test]
    public async Task Anonymous_ReturnsUnauthorized()
    {
        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(Role.Buyer)]
    [TestCase(Role.Admin)]
    public async Task NonMerchant_ReturnsForbidden(Role role)
    {
        await AuthenticateAsync(role);

        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Subscribe(_category.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task NotOnboardedMerchant_ReturnsNotFound()
    {
        await AuthenticateAsync(Role.Merchant);

        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Subscribe(_category.Id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Subscribe_ListAndRemove_RoundTrip()
    {
        await OnboardMerchantAsync();

        var categoryLevel = await Subscribe(_category.Id);
        var tagLevel = await Subscribe(_category.Id, _tag.Id);

        categoryLevel.StatusCode.ShouldBe(HttpStatusCode.Created);
        tagLevel.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await ReadAsAsync<Subscriptions.Response>(tagLevel))!;
        created.TagId.ShouldBe(_tag.Id);

        var list = (await Client.GetFromJsonAsync<List<Subscriptions.Response>>(Url))!;
        list.Count.ShouldBe(2);
        list.ShouldContain(s => s.TagId == null && s.CategoryId == _category.Id);

        (await Client.DeleteAsync($"{Url}/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.GetFromJsonAsync<List<Subscriptions.Response>>(Url))!.Count.ShouldBe(1);
        (await Client.DeleteAsync($"{Url}/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Subscribe_Duplicate_ReturnsConflict()
    {
        await OnboardMerchantAsync();
        await Subscribe(_category.Id);
        await Subscribe(_category.Id, _tag.Id);

        (await Subscribe(_category.Id)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Subscribe(_category.Id, _tag.Id)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Subscribe_InvalidTargets_ReturnBadRequest()
    {
        await OnboardMerchantAsync();
        var otherCategory = new Category(Guid.NewGuid(), "Video", false, DateTimeOffset.UtcNow);
        var disabledTag = new Tag(Guid.NewGuid(), _category.Id, "Old", true, DateTimeOffset.UtcNow);
        var disabledCategory = new Category(Guid.NewGuid(), "Retired", true, DateTimeOffset.UtcNow);
        using (var scope = IntegrationTestSetup.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Categories.AddRange(otherCategory, disabledCategory);
            db.Tags.Add(disabledTag);
            await db.SaveChangesAsync();
        }

        (await Subscribe(Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Subscribe(_category.Id, Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Subscribe(otherCategory.Id, _tag.Id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Subscribe(_category.Id, disabledTag.Id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Subscribe(disabledCategory.Id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Remove_AnotherMerchantsSubscription_ReturnsNotFound()
    {
        await OnboardMerchantAsync();
        var mine = (await ReadAsAsync<Subscriptions.Response>(await Subscribe(_category.Id)))!;

        await OnboardMerchantAsync();

        (await Client.DeleteAsync($"{Url}/{mine.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetFromJsonAsync<List<Subscriptions.Response>>(Url))!.ShouldBeEmpty();
    }
}