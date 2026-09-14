using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// Verifies the CORS policy configured in Program.cs: requests from an allowed origin get an
/// <c>Access-Control-Allow-Origin</c> response header, requests from a non-configured origin
/// don't, and credentials are never allowed (per the CORS DoD in issue #26).
/// </summary>
public class CorsTests : IntegrationTestBase
{
    private const string AllowedOrigin = "http://localhost:3000";
    private const string DisallowedOrigin = "http://evil-site.example.com";

    [Test]
    public async Task Request_FromAllowedOrigin_ReturnsAccessControlAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await Client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            .ShouldBeTrue();
        values!.ShouldContain(AllowedOrigin);
    }

    [Test]
    public async Task Request_FromDisallowedOrigin_DoesNotReturnAccessControlAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", DisallowedOrigin);

        var response = await Client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Test]
    public async Task Response_NeverAllowsCredentials()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await Client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }
}
