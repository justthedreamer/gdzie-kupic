namespace Gdzie.Kupic.Tests.Unit.Location;

using Gdzie.Kupic.Location;
using Gdzie.Kupic.Location.Google;
using Moq;
using Shouldly;

[TestFixture]
public class LocationServiceTests
{
    [TestCase(""), TestCase(null)]
    public async Task Service_ShouldReturn_ValidationError_WhenCoordinatesAreEmptyOrNull(string? value)
    {
        var sut = new Fixture();

        var (_, validationError, _) =
            await sut.WhenExecutingGetLocationAsync(string.Empty, value);

        validationError.ShouldNotBeNull();
    }

    [TestCase(""), TestCase(null)]
    public async Task Service_ShouldReturn_ValidationError_WhenLongitudeIsNullOrEmpty(string? value)
    {
        var sut = new Fixture();

        var (_, validationError, _) =
            await sut.WhenExecutingGetLocationAsync(string.Empty, Guid.NewGuid().ToString());

        validationError.ShouldNotBeNull();
    }

    [TestCase(""), TestCase(null)]
    public async Task Service_ShouldReturn_ValidationError_WhenLatitudeIsNullOrEmpty(string? value)
    {
        var sut = new Fixture();

        var (_, validationError, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), value);

        validationError.ShouldNotBeNull();
    }

    [Test]
    public async Task Service_Returns_InternalErrorTrue_WhenGoogleClientReturns_InternalError()
    {
        var sut = new Fixture()
            .WithInternalErrorGoogleClientResponse();

        var (_, _, internalError) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        internalError.ShouldBeTrue();
    }

    [Test]
    public async Task Service_Returns_InternalErrorTrue_WhenGoogleClientReturns_ThirdPartyError()
    {
        var sut = new Fixture()
            .WithThirdPartyErrorGoogleClientResponse();

        var (_, _, internalError) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        internalError.ShouldBeTrue();
    }

    [Test]
    public async Task Service_Returns_NullLocation_WhenGoogleClientReturns_EmptyResponseResults()
    {
        var sut = new Fixture()
            .WithGoogleClientResponse(new ReverseGeocoding.Response([]));

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());


        location.ShouldBeNull();
    }

    [Test]
    public async Task Service_Returns_UnknownVoivodeship_WhenGoogleClientReturnsResponse_EmptyVoivodeship()
    {
        var sut = new Fixture()
            .WithAddressComponents(
                Fixture.Components.Country,
                Fixture.Components.PostalCode,
                Fixture.Components.City);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.Voivodeship.ShouldBe(sut.Unknown);
    }

    [Test]
    public async Task Service_Returns_UnknownPostalCode_WhenGoogleClientReturnsResponse_EmptyPostalCode()
    {
        var sut = new Fixture()
            .WithAddressComponents(
                Fixture.Components.Voivodeship,
                Fixture.Components.Country,
                Fixture.Components.City);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.PostalCode.ShouldBe(sut.Unknown);
    }

    [Test]
    public async Task Service_Returns_UnknownCity_WhenGoogleClientReturnsResponse_EmptyCity()
    {
        var sut = new Fixture()
            .WithAddressComponents(
                Fixture.Components.Voivodeship,
                Fixture.Components.PostalCode,
                Fixture.Components.Country);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.City.ShouldBe(sut.Unknown);
    }

    [Test]
    public async Task Service_Returns_UnknownCountry_WhenGoogleClientReturnsResponse_EmptyCountry()
    {
        var sut = new Fixture()
            .WithAddressComponents(
                Fixture.Components.Voivodeship,
                Fixture.Components.PostalCode,
                Fixture.Components.City);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.Country.ShouldBe(sut.Unknown);
    }

    [Test]
    public async Task
        Service_Returns_Voivodeship_WhenGoogleClientReturnsResponseResult_With_AdministrativeAreaLevel1_Tag()
    {
        var sut = new Fixture()
            .WithAddressComponents(Fixture.Components.Voivodeship);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.Voivodeship.ShouldNotBeNullOrEmpty();
        location.Voivodeship.ShouldNotBe(sut.Unknown);
    }

    [Test]
    public async Task Service_Returns_PostalCode_WhenGoogleClientReturnsResponseResult_With_PostalCode_Tag()
    {
        var sut = new Fixture()
            .WithAddressComponents(Fixture.Components.PostalCode);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.PostalCode.ShouldNotBeNullOrEmpty();
        location.PostalCode.ShouldNotBe(sut.Unknown);
    }

    [Test]
    public async Task Service_Returns_City_WhenGoogleClientReturnsResponseResult_With_Locality_Tag()
    {
        var sut = new Fixture()
            .WithAddressComponents(Fixture.Components.City);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.City.ShouldNotBeNullOrEmpty();
        location.City.ShouldNotBe(sut.Unknown);
    }

    [Test]
    public async Task Service_Returns_Country_WhenGoogleClientReturnsResponseResult_With_Country_Tag()
    {
        var sut = new Fixture()
            .WithAddressComponents(Fixture.Components.Country);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.Country.ShouldNotBeNullOrEmpty();
        location.Country.ShouldNotBe(sut.Unknown);
    }

    [Test]
    public async Task Service_Returns_LocationData_WhenGoogleClientReturnsResponseResult_With_All_Tags()
    {
        var sut = new Fixture()
            .WithAddressComponents(
                Fixture.Components.Voivodeship,
                Fixture.Components.PostalCode,
                Fixture.Components.City,
                Fixture.Components.Country);

        var (location, _, _) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        location.ShouldNotBeNull();
        location.Voivodeship.ShouldNotBeNullOrEmpty();
        location.Voivodeship.ShouldNotBe(sut.Unknown);
        location.PostalCode.ShouldNotBeNullOrEmpty();
        location.PostalCode.ShouldNotBe(sut.Unknown);
        location.City.ShouldNotBeNullOrEmpty();
        location.City.ShouldNotBe(sut.Unknown);
        location.Country.ShouldNotBeNullOrEmpty();
        location.Country.ShouldNotBe(sut.Unknown);
    }

    [Test]
    public async Task Service_ShouldNotReturn_Errors_WhenGoogleClientReturnsResponseResult_With_All_Tags()
    {
        var sut = new Fixture()
            .WithAddressComponents(
                Fixture.Components.Voivodeship,
                Fixture.Components.PostalCode,
                Fixture.Components.City,    
                Fixture.Components.Country);

        var (_, validationError, internalError) =
            await sut.WhenExecutingGetLocationAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        validationError.ShouldBeNull();
        internalError.ShouldBeFalse();
    }


    private class Fixture
    {
        public readonly string Unknown = "Unknown";

        private readonly Mock<Microsoft.Extensions.Logging.ILogger<LocationService>> _loggerMock = new();
        private readonly Mock<IGoogleGeocodingHttpClient> _clientMock = new();

        private readonly LocationService _service;

        public Fixture()
        {
            this._service = new LocationService(this._loggerMock.Object, this._clientMock.Object);
        }

        public async Task<(ILocationService.LocationData? Location, string? ValidationError, bool InternalError)>
            WhenExecutingGetLocationAsync(string longitude, string latitude)
        {
            return await this._service.GetLocationAsync(longitude, latitude);
        }

        public Fixture WithGoogleClientResponse(
            ReverseGeocoding.Response response = default!,
            bool thirdPartyError = false,
            bool internalError = false)
        {
            this._clientMock
                .Setup(c => c.ReverseGeocodeAsync(It.IsAny<ReverseGeocoding.Request>()))
                .ReturnsAsync(() => (response, thirdPartyError, internalError));

            return this;
        }

        public Fixture WithThirdPartyErrorGoogleClientResponse()
        {
            this._clientMock
                .Setup(c => c.ReverseGeocodeAsync(It.IsAny<ReverseGeocoding.Request>()))
                .ReturnsAsync(() => (default(ReverseGeocoding.Response), true, false));

            return this;
        }

        public Fixture WithInternalErrorGoogleClientResponse()
        {
            this._clientMock
                .Setup(c => c.ReverseGeocodeAsync(It.IsAny<ReverseGeocoding.Request>()))
                .ReturnsAsync(() => (default(ReverseGeocoding.Response), false, true));

            return this;
        }


        public Fixture WithAddressComponents(params ReverseGeocoding.Response.AddressComponent[] addressComponents)
        {
            var response = new ReverseGeocoding.Response([
                new ReverseGeocoding.Response.ResponseResults(addressComponents),
            ]);

            this._clientMock
                .Setup(c => c.ReverseGeocodeAsync(It.IsAny<ReverseGeocoding.Request>()))
                .ReturnsAsync(() => (response, false, false));

            return this;
        }

        public static class Components
        {
            public static ReverseGeocoding.Response.AddressComponent Voivodeship =>
                new("LongText", "ShortText", ["administrative_area_level_1"]);

            public static ReverseGeocoding.Response.AddressComponent PostalCode =>
                new("LongText", "ShortText", ["postal_code"]);

            public static ReverseGeocoding.Response.AddressComponent City =>
                new("LongText", "ShortText", ["locality"]);

            public static ReverseGeocoding.Response.AddressComponent Country =>
                new("LongText", "ShortText", ["country"]);
        }
    }
}