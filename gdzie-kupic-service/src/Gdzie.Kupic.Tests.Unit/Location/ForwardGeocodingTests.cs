using Gdzie.Kupic.Location;
using Gdzie.Kupic.Location.Google;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Location;

public class ForwardGeocodingTests
{
    private Mock<IGoogleGeocodingHttpClient> _client = null!;
    private LocationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _client = new Mock<IGoogleGeocodingHttpClient>();
        _service = new LocationService(NullLogger<LocationService>.Instance, _client.Object);
    }

    private void Returns(ForwardGeocoding.Response? response, bool thirdParty = false, bool internalError = false) =>
        _client.Setup(c => c.ForwardGeocodeAsync(It.IsAny<ForwardGeocoding.Request>()))
            .ReturnsAsync((response, thirdParty, internalError));

    [Test]
    public async Task GeocodeAddress_WithResult_ReturnsCoordinates()
    {
        Returns(new ForwardGeocoding.Response([
            new ForwardGeocoding.Response.ResponseResult(new ForwardGeocoding.Response.LatLng(50.1, 19.9), "Formatted")]));

        var result = await _service.GeocodeAddressAsync("Rynek 1");

        result.IsSuccess.ShouldBeTrue();
        result.Address!.Latitude.ShouldBe(50.1);
        result.Address.Longitude.ShouldBe(19.9);
        result.Address.FormattedAddress.ShouldBe("Formatted");
    }

    [Test]
    public async Task GeocodeAddress_NoResults_ReturnsNotFound()
    {
        Returns(new ForwardGeocoding.Response([]));

        (await _service.GeocodeAddressAsync("x")).Failure.ShouldBe(GeocodeFailure.NotFound);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task GeocodeAddress_ProviderFailure_ReturnsProviderError(bool thirdParty, bool internalError)
    {
        Returns(null, thirdParty, internalError);

        (await _service.GeocodeAddressAsync("x")).Failure.ShouldBe(GeocodeFailure.ProviderError);
    }
}