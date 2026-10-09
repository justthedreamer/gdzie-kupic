using Gdzie.Kupic.Location;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Marketplace;

public class MerchantServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private AppDbContext _db = null!;
    private Mock<ILocationService> _geocoder = null!;
    private MerchantService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _geocoder = new Mock<ILocationService>();
        _service = new MerchantService(new MarketplaceStorage(_db), new LocationInputResolver(_geocoder.Object));
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private static OnboardingInput Input(
        string? name = "Media Expert",
        string? branchName = "Media Expert Krakow",
        double? lat = 50.06,
        double? lon = 19.94,
        string? address = null,
        string? website = null) =>
        new(name, "Electronics", new BranchInput(branchName, "123456789", website, lat, lon, address));

    private async Task AssertNothingStoredAsync()
    {
        (await _db.Merchants.CountAsync()).ShouldBe(0);
        (await _db.MerchantAccounts.CountAsync()).ShouldBe(0);
        (await _db.MerchantBranches.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Onboard_WithCoordinates_CreatesMerchantAccountAndBranch()
    {
        var result = await _service.OnboardAsync(UserId, Input());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("Media Expert");
        var branch = result.Value.Branches.Single();
        branch.Coordinates.Latitude.ShouldBe(50.06);
        branch.AddressDisplayName.ShouldBeNull();
        (await _db.MerchantAccounts.SingleAsync()).UserId.ShouldBe(UserId);
        (await _db.MerchantBranches.SingleAsync()).MerchantId.ShouldBe(result.Value.Id);
        _geocoder.Verify(g => g.GeocodeAddressAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Onboard_WithAddress_GeocodesOnceAndStoresFormattedAddress()
    {
        _geocoder.Setup(g => g.GeocodeAddressAsync("Rynek 1"))
            .ReturnsAsync(new GeocodeResult(new GeocodedAddress(50.0617, 19.9373, "Rynek Glowny 1, Krakow"), GeocodeFailure.None));

        var result = await _service.OnboardAsync(UserId, Input(lat: null, lon: null, address: "Rynek 1"));

        result.IsSuccess.ShouldBeTrue();
        var branch = await _db.MerchantBranches.SingleAsync();
        branch.Coordinates.Latitude.ShouldBe(50.0617);
        branch.AddressDisplayName.ShouldBe("Rynek Glowny 1, Krakow");
        _geocoder.Verify(g => g.GeocodeAddressAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Onboard_GeocodingFailure_StoresNothing()
    {
        _geocoder.Setup(g => g.GeocodeAddressAsync(It.IsAny<string>()))
            .ReturnsAsync(new GeocodeResult(null, GeocodeFailure.ProviderError));

        var result = await _service.OnboardAsync(UserId, Input(lat: null, lon: null, address: "Rynek 1"));

        result.Error.ShouldBe(MerchantError.GeocodingFailed);
        await AssertNothingStoredAsync();
    }

    [Test]
    public async Task Onboard_AddressNotFound_ReturnsValidationAndStoresNothing()
    {
        _geocoder.Setup(g => g.GeocodeAddressAsync(It.IsAny<string>()))
            .ReturnsAsync(new GeocodeResult(null, GeocodeFailure.NotFound));

        var result = await _service.OnboardAsync(UserId, Input(lat: null, lon: null, address: "nowhere"));

        result.Error.ShouldBe(MerchantError.Validation);
        await AssertNothingStoredAsync();
    }

    [Test]
    public async Task Onboard_Twice_ReturnsAlreadyOnboardedAndKeepsSingleMerchant()
    {
        await _service.OnboardAsync(UserId, Input());

        var second = await _service.OnboardAsync(UserId, Input(name: "Other"));

        second.Error.ShouldBe(MerchantError.AlreadyOnboarded);
        (await _db.Merchants.CountAsync()).ShouldBe(1);
        (await _db.MerchantBranches.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Onboard_DifferentUsers_CreateSeparateMerchants()
    {
        await _service.OnboardAsync(UserId, Input());
        var other = await _service.OnboardAsync(Guid.NewGuid(), Input());

        other.IsSuccess.ShouldBeTrue();
        (await _db.Merchants.CountAsync()).ShouldBe(2);
    }

    [TestCase(null, "Branch", null, null, "Rynek", null)]
    [TestCase("  ", "Branch", 50.0, 19.0, null, null)]
    [TestCase("Name", " ", 50.0, 19.0, null, null)]
    [TestCase("Name", "Branch", null, null, null, null)]
    [TestCase("Name", "Branch", 50.0, 19.0, "Rynek", null)]
    [TestCase("Name", "Branch", 95.0, 19.0, null, null)]
    [TestCase("Name", "Branch", 50.0, 19.0, null, "not a url")]
    [TestCase("Name", "Branch", 50.0, 19.0, null, "ftp://example.com")]
    public async Task Onboard_InvalidInput_ReturnsValidationAndStoresNothing(
        string? name, string? branchName, double? lat, double? lon, string? address, string? website)
    {
        var result = await _service.OnboardAsync(UserId, Input(name, branchName, lat, lon, address, website));

        result.Error.ShouldBe(MerchantError.Validation);
        await AssertNothingStoredAsync();
    }

    [Test]
    public async Task Onboard_MissingBranch_ReturnsValidation()
    {
        var result = await _service.OnboardAsync(UserId, new OnboardingInput("Name", null, null));

        result.Error.ShouldBe(MerchantError.Validation);
    }

    [Test]
    public async Task GetMe_BeforeOnboarding_ReturnsNotOnboarded()
    {
        (await _service.GetMeAsync(UserId)).Error.ShouldBe(MerchantError.NotOnboarded);
    }

    [Test]
    public async Task GetMe_AfterOnboarding_ReturnsMerchantWithBranch()
    {
        await _service.OnboardAsync(UserId, Input(website: "https://example.com"));

        var result = await _service.GetMeAsync(UserId);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Branches.Single().Website.ShouldBe("https://example.com");
    }
}
