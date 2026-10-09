using Gdzie.Kupic.Location;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Location;

public class SavedLocationServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private AppDbContext _db = null!;
    private Mock<ILocationService> _geocoder = null!;
    private SavedLocationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _geocoder = new Mock<ILocationService>();
        _service = new SavedLocationService(new LocationStorage(_db), _geocoder.Object);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Create_WithCoordinates_SavesWithoutGeocoding()
    {
        var result = await _service.CreateAsync(UserId, "Home", 50.06, 19.94, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Coordinates.Latitude.ShouldBe(50.06);
        result.Value.Coordinates.Longitude.ShouldBe(19.94);
        _geocoder.Verify(g => g.GeocodeAddressAsync(It.IsAny<string>()), Times.Never);
        (await _db.SavedLocations.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Create_WithAddress_GeocodesOnceAndStoresCoordinates()
    {
        _geocoder.Setup(g => g.GeocodeAddressAsync("Rynek 1, Krakow"))
            .ReturnsAsync(new GeocodeResult(new GeocodedAddress(50.0617, 19.9373, "Rynek Glowny 1"), GeocodeFailure.None));

        var result = await _service.CreateAsync(UserId, "Office", null, null, "  Rynek 1, Krakow ");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Coordinates.Latitude.ShouldBe(50.0617);
        result.Value.Coordinates.Longitude.ShouldBe(19.9373);
        _geocoder.Verify(g => g.GeocodeAddressAsync(It.IsAny<string>()), Times.Once);
    }

    [TestCase(50.0, 19.0, "Rynek 1")]
    [TestCase(null, null, null)]
    [TestCase(null, null, "   ")]
    [TestCase(50.0, null, null)]
    [TestCase(null, 19.0, null)]
    [TestCase(91.0, 19.0, null)]
    [TestCase(-91.0, 19.0, null)]
    [TestCase(50.0, 181.0, null)]
    [TestCase(50.0, -181.0, null)]
    public async Task Create_InvalidLocationInput_ReturnsValidationError(double? lat, double? lon, string? address)
    {
        var result = await _service.CreateAsync(UserId, "Home", lat, lon, address);

        result.Error.ShouldBe(SavedLocationError.Validation);
        (await _db.SavedLocations.CountAsync()).ShouldBe(0);
        _geocoder.Verify(g => g.GeocodeAddressAsync(It.IsAny<string>()), Times.Never);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Create_BlankName_ReturnsValidationError(string? name)
    {
        var result = await _service.CreateAsync(UserId, name, 50.0, 19.0, null);

        result.Error.ShouldBe(SavedLocationError.Validation);
    }

    [Test]
    public async Task Create_AddressNotFound_ReturnsValidationErrorAndStoresNothing()
    {
        _geocoder.Setup(g => g.GeocodeAddressAsync(It.IsAny<string>()))
            .ReturnsAsync(new GeocodeResult(null, GeocodeFailure.NotFound));

        var result = await _service.CreateAsync(UserId, "Home", null, null, "nowhere");

        result.Error.ShouldBe(SavedLocationError.Validation);
        (await _db.SavedLocations.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Create_ProviderFailure_ReturnsGeocodingFailedAndStoresNothing()
    {
        _geocoder.Setup(g => g.GeocodeAddressAsync(It.IsAny<string>()))
            .ReturnsAsync(new GeocodeResult(null, GeocodeFailure.ProviderError));

        var result = await _service.CreateAsync(UserId, "Home", null, null, "Rynek 1");

        result.Error.ShouldBe(SavedLocationError.GeocodingFailed);
        (await _db.SavedLocations.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Get_ReturnsOnlyCallersLocations()
    {
        var other = Guid.NewGuid();
        await _service.CreateAsync(UserId, "Mine", 50.0, 19.0, null);
        await _service.CreateAsync(other, "Theirs", 51.0, 20.0, null);

        var result = await _service.GetAsync(UserId);

        result.Select(l => l.DisplayName).ShouldBe(["Mine"]);
    }

    [Test]
    public async Task Delete_OwnLocation_Succeeds_OtherUsersLocation_ReturnsFalse()
    {
        var mine = (await _service.CreateAsync(UserId, "Mine", 50.0, 19.0, null)).Value!;
        var theirs = (await _service.CreateAsync(Guid.NewGuid(), "Theirs", 51.0, 20.0, null)).Value!;

        (await _service.DeleteAsync(UserId, theirs.Id)).ShouldBeFalse();
        (await _service.DeleteAsync(UserId, mine.Id)).ShouldBeTrue();

        (await _db.SavedLocations.CountAsync()).ShouldBe(1);
    }
}