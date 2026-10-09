using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Auth;
using Gdzie.Kupic.Service.API.Contract.Catalogue;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class CatalogueControllerTests : IntegrationTestBase
{
    private const string ListUrl = "/api/catalogue/categories";

    [Test]
    public async Task GetCategories_WithoutToken_ReturnsUnauthorized()
    {
        (await Client.GetAsync(ListUrl)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(Role.Buyer)]
    [TestCase(Role.Merchant)]
    [TestCase(Role.Admin)]
    public async Task GetCategories_AnyAuthenticatedRole_ReturnsOk(Role role)
    {
        await AuthenticateAsync(role);

        (await Client.GetAsync(ListUrl)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase(Role.Buyer)]
    [TestCase(Role.Merchant)]
    public async Task AdminEndpoints_NonAdmin_ReturnForbidden(Role role)
    {
        await AuthenticateAsync(role);
        var id = Guid.NewGuid();

        (await Client.PostAsJsonAsync("/api/admin/categories", new NameRequest("X"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PutAsJsonAsync($"/api/admin/categories/{id}", new NameRequest("X"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PostAsync($"/api/admin/categories/{id}/disable", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PostAsJsonAsync($"/api/admin/categories/{id}/tags", new NameRequest("X"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PutAsJsonAsync($"/api/admin/tags/{id}", new NameRequest("X"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PostAsync($"/api/admin/tags/{id}/enable", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task AdminEndpoints_Anonymous_ReturnUnauthorized()
    {
        (await Client.PostAsJsonAsync("/api/admin/categories", new NameRequest("X"))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Admin_CanCreateRenameDisableAndReadBack()
    {
        await AuthenticateAsync(Role.Admin);

        var created = await Client.PostAsJsonAsync("/api/admin/categories", new NameRequest("Audio"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var category = (await ReadAsAsync<CategoryResponse>(created))!;

        var tagResponse = await Client.PostAsJsonAsync($"/api/admin/categories/{category.Id}/tags", new NameRequest("Mic"));
        tagResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tag = (await ReadAsAsync<TagResponse>(tagResponse))!;

        (await Client.PutAsJsonAsync($"/api/admin/tags/{tag.Id}", new NameRequest("Microphone"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Client.PostAsync($"/api/admin/tags/{tag.Id}/disable", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = (await Client.GetFromJsonAsync<List<CategoryResponse>>(ListUrl))!;
        var listed = list.Single(c => c.Id == category.Id).Tags.Single();
        listed.Name.ShouldBe("Microphone");
        listed.IsDisabled.ShouldBeTrue();
    }

    [Test]
    public async Task Admin_DuplicatesReturnConflict_UnknownReturnsNotFound()
    {
        await AuthenticateAsync(Role.Admin);
        var category = (await ReadAsAsync<CategoryResponse>(
            await Client.PostAsJsonAsync("/api/admin/categories", new NameRequest("Audio"))))!;
        await Client.PostAsJsonAsync($"/api/admin/categories/{category.Id}/tags", new NameRequest("Mic"));

        (await Client.PostAsJsonAsync("/api/admin/categories", new NameRequest("Audio"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Client.PostAsJsonAsync($"/api/admin/categories/{category.Id}/tags", new NameRequest("Mic"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Client.PostAsync($"/api/admin/categories/{Guid.NewGuid()}/enable", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync($"/api/admin/tags/{Guid.NewGuid()}", new NameRequest("X"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
