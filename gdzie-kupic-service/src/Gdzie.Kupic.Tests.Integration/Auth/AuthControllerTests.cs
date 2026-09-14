using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Service.API.Contract.Auth;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration.Auth;

public class AuthControllerTests : IntegrationTestBase
{
    [Test]
    public async Task SignUp_WithValidData_ReturnsTokens_AndPersistsUserWithRole()
    {
        var request = new SignUp.Request
        {
            Email = "buyer@example.com",
            Password = "password123",
            Role = nameof(Role.Buyer),
        };

        var response = await Client.PostAsJsonAsync("/auth/sign-up", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsAsync<SignUp.Response>(response);
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBeNullOrEmpty();

        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "buyer@example.com");
        user.Role.ShouldBe(Role.Buyer);
        user.PasswordHash.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task SignIn_WithValidCredentials_ReturnsTokens()
    {
        await Client.PostAsJsonAsync("/auth/sign-up", new SignUp.Request
        {
            Email = "buyer@example.com",
            Password = "password123",
            Role = nameof(Role.Buyer),
        });

        var response = await Client.PostAsJsonAsync("/auth/sign-in",
            new SignIn.Request("buyer@example.com", "password123"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsAsync<SignIn.Response>(response);
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBeNullOrEmpty();
    }
}
