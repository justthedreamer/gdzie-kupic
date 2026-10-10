using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Service.API.Contract.Account;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class NotificationSettingsControllerTests : IntegrationTestBase
{
    private const string Url = "/api/account/notification-settings";

    [TestCase(Role.Buyer)]
    [TestCase(Role.Merchant)]
    public async Task Default_IsOff_AndCanBeSwitchedOnAndOff(Role role)
    {
        await AuthenticateAsync(role);

        (await ReadAsAsync<NotificationSettings.Response>(await Client.GetAsync(Url)))!.EmailEnabled.ShouldBeFalse();

        var on = await Client.PutAsJsonAsync(Url, new NotificationSettings.UpdateRequest(true));
        on.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsAsync<NotificationSettings.Response>(on))!.EmailEnabled.ShouldBeTrue();
        (await ReadAsAsync<NotificationSettings.Response>(await Client.GetAsync(Url)))!.EmailEnabled.ShouldBeTrue();

        await Client.PutAsJsonAsync(Url, new NotificationSettings.UpdateRequest(false));
        (await ReadAsAsync<NotificationSettings.Response>(await Client.GetAsync(Url)))!.EmailEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task Setting_IsPerAccount()
    {
        var first = await AuthenticateAsync(Role.Buyer);
        await Client.PutAsJsonAsync(Url, new NotificationSettings.UpdateRequest(true));
        await AuthenticateAsync(Role.Buyer);

        (await ReadAsAsync<NotificationSettings.Response>(await Client.GetAsync(Url)))!.EmailEnabled.ShouldBeFalse();
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Id == first)).EmailNotificationsEnabled.ShouldBeTrue();
    }

    [Test]
    public async Task Admin_GetsForbidden_AndAnonymousUnauthorized()
    {
        using var anonymous = IntegrationTestSetup.Factory.CreateClient();
        (await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await AuthenticateAsync(Role.Admin);

        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PutAsJsonAsync(Url, new NotificationSettings.UpdateRequest(true))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
