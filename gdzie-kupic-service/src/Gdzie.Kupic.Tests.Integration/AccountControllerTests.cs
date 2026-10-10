using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Service.API.Contract.Account;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class AccountControllerTests : IntegrationTestBase
{
    private const string Url = "/api/account/profile";

    [Test]
    public async Task Anonymous_IsRejected()
    {
        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Client.PutAsJsonAsync(Url, new Profile.UpdateRequest("Anna"))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(Role.Buyer)]
    [TestCase(Role.Merchant)]
    public async Task Get_ReturnsTheCallersEmailRoleAndNoNameYet(Role role)
    {
        await AuthenticateAsync(role);

        var response = await Client.GetAsync(Url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = (await ReadAsAsync<Profile.Response>(response))!;
        profile.Email.ShouldEndWith("@example.com");
        profile.Role.ShouldBe(role.ToString());
        profile.FirstName.ShouldBeNull();
    }

    [Test]
    public async Task Put_SetsTheFirstName_AndGetReturnsIt()
    {
        var userId = await AuthenticateAsync(Role.Buyer);

        var response = await Client.PutAsJsonAsync(Url, new Profile.UpdateRequest("  Anna  Maria "));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsAsync<Profile.Response>(response))!.FirstName.ShouldBe("Anna Maria");
        (await StoredNameAsync(userId)).ShouldBe("Anna Maria");

        var get = (await ReadAsAsync<Profile.Response>(await Client.GetAsync(Url)))!;
        get.FirstName.ShouldBe("Anna Maria");
    }

    [Test]
    public async Task Put_WithAnEmptyName_ClearsIt()
    {
        var userId = await AuthenticateAsync(Role.Buyer);
        await Client.PutAsJsonAsync(Url, new Profile.UpdateRequest("Anna"));

        var response = await Client.PutAsJsonAsync(Url, new Profile.UpdateRequest(""));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsAsync<Profile.Response>(response))!.FirstName.ShouldBeNull();
        (await StoredNameAsync(userId)).ShouldBeNull();
    }

    [TestCase("Anna1")]
    [TestCase("<b>Anna</b>")]
    [TestCase("anna@example.com")]
    public async Task Put_WithAnInvalidName_IsRejected_AndKeepsTheOldOne(string name)
    {
        var userId = await AuthenticateAsync(Role.Buyer);
        await Client.PutAsJsonAsync(Url, new Profile.UpdateRequest("Anna"));

        var response = await Client.PutAsJsonAsync(Url, new Profile.UpdateRequest(name));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await StoredNameAsync(userId)).ShouldBe("Anna");
    }

    [Test]
    public async Task Put_WithATooLongName_IsRejected()
    {
        await AuthenticateAsync(Role.Buyer);

        var response = await Client.PutAsJsonAsync(Url, new Profile.UpdateRequest(new string('a', 51)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<string?> StoredNameAsync(Guid userId)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).FirstName;
    }
}
