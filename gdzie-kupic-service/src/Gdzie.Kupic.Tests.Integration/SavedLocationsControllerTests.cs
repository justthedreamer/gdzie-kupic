using System.Net;
using System.Net.Http.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Location.Google;
using Gdzie.Kupic.Service.API.Contract.Location;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class SavedLocationsControllerTests : IntegrationTestBase
{
    private const string Url = "/api/saved-locations";

    [Test]
    public async Task Anonymous_ReturnsUnauthorized()
    {
        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(Role.Merchant)]
    [TestCase(Role.Admin)]
    public async Task NonBuyer_ReturnsForbidden(Role role)
    {
        await AuthenticateAsync(role);

        (await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", 50, 19, null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Create_WithCoordinates_ReturnsCreatedWithoutGeocoding()
    {
        await AuthenticateAsync(Role.Buyer);

        var response = await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", 50.06, 19.94, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await ReadAsAsync<SavedLocations.Response>(response))!;
        body.DisplayName.ShouldBe("Home");
        body.Latitude.ShouldBe(50.06);
        body.Longitude.ShouldBe(19.94);
        Geocoder.ForwardCalls.ShouldBe(0);
    }

    [Test]
    public async Task Create_WithCoordinates_StoresReadableAddressLabel()
    {
        await AuthenticateAsync(Role.Buyer);
        Geocoder.Reverse = _ => (new ReverseGeocoding.Response([
            new ReverseGeocoding.Response.ResponseResults([
                new ReverseGeocoding.Response.AddressComponent("31-042", "31-042", ["postal_code"]),
                new ReverseGeocoding.Response.AddressComponent("Krakow", "Krakow", ["locality"]),
                new ReverseGeocoding.Response.AddressComponent("Polska", "PL", ["country"])])]), false, false);

        var response = await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", 50.06, 19.94, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadAsAsync<SavedLocations.Response>(response))!.AddressDisplayName.ShouldBe("31-042 Krakow, Polska");
        (await Client.GetFromJsonAsync<List<SavedLocations.Response>>(Url))!.Single().AddressDisplayName.ShouldBe("31-042 Krakow, Polska");
    }

    [Test]
    public async Task Create_WithCoordinates_ReverseGeocodingFailure_StillCreatesWithoutLabel()
    {
        await AuthenticateAsync(Role.Buyer);
        Geocoder.Reverse = _ => (new ReverseGeocoding.Response([]), true, false);

        var response = await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", 50.06, 19.94, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadAsAsync<SavedLocations.Response>(response))!.AddressDisplayName.ShouldBeNull();
    }

    [Test]
    public async Task Create_WithAddress_GeocodesOnceAndStoresResolvedCoordinates()
    {
        await AuthenticateAsync(Role.Buyer);
        Geocoder.Forward = _ => (new ForwardGeocoding.Response([
            new ForwardGeocoding.Response.ResponseResult(new ForwardGeocoding.Response.LatLng(52.23, 21.01), "Warszawa")]), false, false);

        var response = await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Office", null, null, "Warszawa"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await ReadAsAsync<SavedLocations.Response>(response))!;
        body.Latitude.ShouldBe(52.23);
        body.Longitude.ShouldBe(21.01);
        Geocoder.ForwardCalls.ShouldBe(1);
    }

    [Test]
    public async Task Create_InvalidInput_ReturnsBadRequest()
    {
        await AuthenticateAsync(Role.Buyer);

        (await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", 50, 19, "Warszawa"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", null, null, null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest(" ", 50, 19, null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", 123, 19, null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_AddressNotFound_ReturnsBadRequestAndStoresNothing()
    {
        await AuthenticateAsync(Role.Buyer);

        var response = await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", null, null, "nowhere"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.GetFromJsonAsync<List<SavedLocations.Response>>(Url))!.ShouldBeEmpty();
    }

    [Test]
    public async Task Create_ProviderFailure_ReturnsBadGatewayAndStoresNothing()
    {
        await AuthenticateAsync(Role.Buyer);
        Geocoder.Forward = _ => (null, true, false);

        var response = await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Home", null, null, "Warszawa"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await Client.GetFromJsonAsync<List<SavedLocations.Response>>(Url))!.ShouldBeEmpty();
    }

    [Test]
    public async Task List_AndDelete_AreScopedToTheCaller()
    {
        await AuthenticateAsync(Role.Buyer);
        var mine = (await ReadAsAsync<SavedLocations.Response>(
            await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Mine", 50, 19, null))))!;

        await AuthenticateAsync(Role.Buyer);
        var theirs = (await ReadAsAsync<SavedLocations.Response>(
            await Client.PostAsJsonAsync(Url, new SavedLocations.CreateRequest("Theirs", 51, 20, null))))!;

        (await Client.GetFromJsonAsync<List<SavedLocations.Response>>(Url))!.Select(l => l.Id).ShouldBe([theirs.Id]);
        (await Client.DeleteAsync($"{Url}/{mine.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.DeleteAsync($"{Url}/{theirs.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.GetFromJsonAsync<List<SavedLocations.Response>>(Url))!.ShouldBeEmpty();
    }
}
