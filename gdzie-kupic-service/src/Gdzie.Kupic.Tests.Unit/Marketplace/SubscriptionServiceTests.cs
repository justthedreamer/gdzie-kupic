using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Marketplace;

public class SubscriptionServiceTests
{
    private AppDbContext _db = null!;
    private SubscriptionService _service = null!;
    private Guid _userId;
    private Guid _merchantId;
    private Category _category = null!;
    private Tag _tag = null!;

    [SetUp]
    public async Task SetUp()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = new SubscriptionService(new MarketplaceStorage(_db), new CatalogueStorage(_db));

        _userId = Guid.NewGuid();
        _merchantId = await AddMerchantAsync(_userId);
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);
        _db.Categories.Add(_category);
        _db.Tags.Add(_tag);
        await _db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task<Guid> AddMerchantAsync(Guid userId)
    {
        var merchant = new Merchant(Guid.NewGuid(), "M", null, DateTimeOffset.UtcNow);
        _db.Merchants.Add(merchant);
        _db.MerchantAccounts.Add(new MerchantAccount(Guid.NewGuid(), merchant.Id, userId, DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync();
        return merchant.Id;
    }

    [Test]
    public async Task Add_CategoryOnly_Succeeds()
    {
        var result = await _service.AddAsync(_userId, _category.Id, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TagId.ShouldBeNull();
        result.Value.MerchantId.ShouldBe(_merchantId);
    }

    [Test]
    public async Task Add_CategoryAndTag_Succeeds()
    {
        var result = await _service.AddAsync(_userId, _category.Id, _tag.Id);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TagId.ShouldBe(_tag.Id);
    }

    [Test]
    public async Task Add_CategoryAndTagLevelAreIndependent()
    {
        (await _service.AddAsync(_userId, _category.Id, null)).IsSuccess.ShouldBeTrue();
        (await _service.AddAsync(_userId, _category.Id, _tag.Id)).IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task Add_DuplicateCategoryLevel_ReturnsDuplicate()
    {
        await _service.AddAsync(_userId, _category.Id, null);

        (await _service.AddAsync(_userId, _category.Id, null)).Error.ShouldBe(SubscriptionError.Duplicate);
    }

    [Test]
    public async Task Add_DuplicateTagLevel_ReturnsDuplicate()
    {
        await _service.AddAsync(_userId, _category.Id, _tag.Id);

        (await _service.AddAsync(_userId, _category.Id, _tag.Id)).Error.ShouldBe(SubscriptionError.Duplicate);
        (await _db.MerchantSubscriptions.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Add_SameSubscriptionForAnotherMerchant_Succeeds()
    {
        var otherUser = Guid.NewGuid();
        await AddMerchantAsync(otherUser);
        await _service.AddAsync(_userId, _category.Id, null);

        (await _service.AddAsync(otherUser, _category.Id, null)).IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task Add_UnknownCategory_ReturnsValidation()
    {
        (await _service.AddAsync(_userId, Guid.NewGuid(), null)).Error.ShouldBe(SubscriptionError.Validation);
    }

    [Test]
    public async Task Add_UnknownTag_ReturnsValidation()
    {
        (await _service.AddAsync(_userId, _category.Id, Guid.NewGuid())).Error.ShouldBe(SubscriptionError.Validation);
    }

    [Test]
    public async Task Add_TagFromAnotherCategory_ReturnsValidation()
    {
        var other = new Category(Guid.NewGuid(), "Video", false, DateTimeOffset.UtcNow);
        _db.Categories.Add(other);
        await _db.SaveChangesAsync();

        (await _service.AddAsync(_userId, other.Id, _tag.Id)).Error.ShouldBe(SubscriptionError.Validation);
    }

    [Test]
    public async Task Add_DisabledCategory_ReturnsValidation()
    {
        _category.IsDisabled = true;
        await _db.SaveChangesAsync();

        (await _service.AddAsync(_userId, _category.Id, null)).Error.ShouldBe(SubscriptionError.Validation);
    }

    [Test]
    public async Task Add_DisabledTag_ReturnsValidation()
    {
        _tag.IsDisabled = true;
        await _db.SaveChangesAsync();

        (await _service.AddAsync(_userId, _category.Id, _tag.Id)).Error.ShouldBe(SubscriptionError.Validation);
    }

    [Test]
    public async Task NotOnboarded_ReturnsNotOnboardedForAllOperations()
    {
        var stranger = Guid.NewGuid();

        (await _service.ListAsync(stranger)).Error.ShouldBe(SubscriptionError.NotOnboarded);
        (await _service.AddAsync(stranger, _category.Id, null)).Error.ShouldBe(SubscriptionError.NotOnboarded);
        (await _service.RemoveAsync(stranger, Guid.NewGuid())).Error.ShouldBe(SubscriptionError.NotOnboarded);
    }

    [Test]
    public async Task List_ReturnsOnlyOwnSubscriptions_AndKeepsDisabledTags()
    {
        var otherUser = Guid.NewGuid();
        await AddMerchantAsync(otherUser);
        await _service.AddAsync(_userId, _category.Id, _tag.Id);
        await _service.AddAsync(otherUser, _category.Id, null);
        _tag.IsDisabled = true;
        await _db.SaveChangesAsync();

        var result = await _service.ListAsync(_userId);

        result.Value!.Single().TagId.ShouldBe(_tag.Id);
    }

    [Test]
    public async Task Remove_OwnSubscription_Succeeds()
    {
        var added = (await _service.AddAsync(_userId, _category.Id, null)).Value!;

        (await _service.RemoveAsync(_userId, added.Id)).IsSuccess.ShouldBeTrue();
        (await _db.MerchantSubscriptions.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Remove_AnotherMerchantsSubscription_ReturnsNotFoundAndKeepsIt()
    {
        var otherUser = Guid.NewGuid();
        await AddMerchantAsync(otherUser);
        var added = (await _service.AddAsync(otherUser, _category.Id, null)).Value!;

        (await _service.RemoveAsync(_userId, added.Id)).Error.ShouldBe(SubscriptionError.NotFound);
        (await _db.MerchantSubscriptions.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Remove_Unknown_ReturnsNotFound()
    {
        (await _service.RemoveAsync(_userId, Guid.NewGuid())).Error.ShouldBe(SubscriptionError.NotFound);
    }
}