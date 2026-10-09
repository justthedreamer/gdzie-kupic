using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Seeding;
using Gdzie.Kupic.Location.Google;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class MerchantOnboardingTests : IntegrationTestBase
{
    private const string MeUrl = "/api/merchant/me";
    private const string OnboardingUrl = "/api/merchant/onboarding";

    private static Onboarding.Request Request(
        double? lat = 50.06, double? lon = 19.94, string? address = null) =>
        new("Media Expert", "Electronics", new Onboarding.BranchRequest("Krakow Galeria", "123456789", "https://example.com", lat, lon, address));

    [Test]
    public async Task Anonymous_ReturnsUnauthorized()
    {
        (await Client.GetAsync(MeUrl)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync(OnboardingUrl, Request())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(Role.Buyer)]
    [TestCase(Role.Admin)]
    public async Task NonMerchant_ReturnsForbidden(Role role)
    {
        await AuthenticateAsync(role);

        (await Client.GetAsync(MeUrl)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PostAsJsonAsync(OnboardingUrl, Request())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Me_BeforeOnboarding_ReturnsNotFound()
    {
        await AuthenticateAsync(Role.Merchant);

        (await Client.GetAsync(MeUrl)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Onboard_WithCoordinates_ReturnsCreatedAndMeReturnsTheSameShape()
    {
        await AuthenticateAsync(Role.Merchant);

        var response = await Client.PostAsJsonAsync(OnboardingUrl, Request());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await ReadAsAsync<MerchantMeResponse>(response))!;
        created.Name.ShouldBe("Media Expert");
        created.Branch.Latitude.ShouldBe(50.06);
        created.Branch.Phone.ShouldBe("123456789");

        var me = (await Client.GetFromJsonAsync<MerchantMeResponse>(MeUrl))!;
        me.MerchantId.ShouldBe(created.MerchantId);
        me.Branch.Id.ShouldBe(created.Branch.Id);
        me.Branch.Website.ShouldBe("https://example.com");
        Geocoder.ForwardCalls.ShouldBe(0);
    }

    [Test]
    public async Task Onboard_WithAddress_UsesGeocodedCoordinatesAndAddressName()
    {
        await AuthenticateAsync(Role.Merchant);
        Geocoder.Forward = _ => (new ForwardGeocoding.Response([
            new ForwardGeocoding.Response.ResponseResult(new ForwardGeocoding.Response.LatLng(52.23, 21.01), "Warszawa, Polska")]), false, false);

        var response = await Client.PostAsJsonAsync(OnboardingUrl, Request(null, null, "Warszawa"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await ReadAsAsync<MerchantMeResponse>(response))!;
        body.Branch.Latitude.ShouldBe(52.23);
        body.Branch.AddressDisplayName.ShouldBe("Warszawa, Polska");
        Geocoder.ForwardCalls.ShouldBe(1);
    }

    [Test]
    public async Task Onboard_Twice_ReturnsConflict()
    {
        await AuthenticateAsync(Role.Merchant);
        await Client.PostAsJsonAsync(OnboardingUrl, Request());

        (await Client.PostAsJsonAsync(OnboardingUrl, Request())).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Onboard_InvalidInput_ReturnsBadRequest()
    {
        await AuthenticateAsync(Role.Merchant);

        (await Client.PostAsJsonAsync(OnboardingUrl, Request(null, null, null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(OnboardingUrl, Request(50, 19, "Warszawa"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(OnboardingUrl, Request() with { Name = " " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Onboard_GeocodingFailure_LeavesNoPartialData()
    {
        await AuthenticateAsync(Role.Merchant);
        Geocoder.Forward = _ => (null, true, false);

        var response = await Client.PostAsJsonAsync(OnboardingUrl, Request(null, null, "Warszawa"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await Client.GetAsync(MeUrl)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The same user can still onboard once the provider recovers.
        Geocoder.Reset();
        (await Client.PostAsJsonAsync(OnboardingUrl, Request())).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task MockMerchantAccount_CanOnboard()
    {
        await AuthenticateAsync(Role.Merchant, MockAccounts.Merchant.Id);

        (await Client.PostAsJsonAsync(OnboardingUrl, Request())).StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
