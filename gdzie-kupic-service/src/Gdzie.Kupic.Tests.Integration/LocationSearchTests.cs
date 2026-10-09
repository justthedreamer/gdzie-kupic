using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Location.Google;
using Gdzie.Kupic.Service.API.Contract.Location;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class LocationSearchTests : IntegrationTestBase
{
    private const string Url = "/api/location/search?address=";

    [Test]
    public async Task Anonymous_ReturnsUnauthorized()
    {
        (await Client.GetAsync(Url + "Krakow")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(Role.Buyer)]
    [TestCase(Role.Merchant)]
    [TestCase(Role.Admin)]
    public async Task AnyAuthenticatedRole_ReturnsResolvedAddress_WithoutStoringAnything(Role role)
    {
        await AuthenticateAsync(role);
        Geocoder.Forward = _ => (new ForwardGeocoding.Response([
            new ForwardGeocoding.Response.ResponseResult(new ForwardGeocoding.Response.LatLng(50.06, 19.94), "Rynek Glowny 1, 31-042 Krakow")]), false, false);

        var response = await Client.GetAsync(Url + Uri.EscapeDataString("Rynek Glowny 1, 31-042 Krakow"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await ReadAsAsync<SearchAddress.Response>(response))!;
        body.Latitude.ShouldBe(50.06);
        body.Longitude.ShouldBe(19.94);
        body.FormattedAddress.ShouldBe("Rynek Glowny 1, 31-042 Krakow");
        Geocoder.ForwardCalls.ShouldBe(1);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task BlankAddress_ReturnsBadRequest_WithoutGeocoding(string address)
    {
        await AuthenticateAsync(Role.Buyer);

        (await Client.GetAsync(Url + Uri.EscapeDataString(address))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Geocoder.ForwardCalls.ShouldBe(0);
    }

    [Test]
    public async Task UnknownAddress_ReturnsNotFound()
    {
        await AuthenticateAsync(Role.Buyer);

        (await Client.GetAsync(Url + "nowhere")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ProviderFailure_ReturnsBadGateway()
    {
        await AuthenticateAsync(Role.Buyer);
        Geocoder.Forward = _ => (null, true, false);

        (await Client.GetAsync(Url + "Krakow")).StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }
}
