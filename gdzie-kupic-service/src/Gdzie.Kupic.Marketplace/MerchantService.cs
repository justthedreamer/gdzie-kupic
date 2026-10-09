namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Location;
using Gdzie.Kupic.Storage;

internal sealed class MerchantService(
    IMarketplaceStorage storage,
    ILocationInputResolver locationResolver) : IMerchantService
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxPhoneLength = 30;
    public const int MaxWebsiteLength = 300;

    public async Task<MerchantResult<Merchant>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var merchant = await storage.FindMerchantByUserIdAsync(userId, ct);

        return merchant is null
            ? new MerchantResult<Merchant>(null, MerchantError.NotOnboarded, "The merchant has not completed onboarding.")
            : new MerchantResult<Merchant>(merchant, MerchantError.None);
    }

    public async Task<MerchantResult<Merchant>> OnboardAsync(Guid userId, OnboardingInput input, CancellationToken ct = default)
    {
        var validationError = Validate(input, out var name, out var description, out var branchName, out var phone, out var website);
        if (validationError is not null) return Fail(MerchantError.Validation, validationError);

        if (await storage.FindMerchantByUserIdAsync(userId, ct) is not null)
            return Fail(MerchantError.AlreadyOnboarded, "This account has already completed onboarding.");

        // Geocoding happens before anything is written, so a provider failure leaves no partial data.
        var branch = input.Branch!;
        var resolved = await locationResolver.ResolveAsync(branch.Latitude, branch.Longitude, branch.Address);
        if (resolved.Error == LocationInputError.Validation) return Fail(MerchantError.Validation, resolved.Message!);
        if (!resolved.IsSuccess) return Fail(MerchantError.GeocodingFailed, resolved.Message!);

        var now = DateTimeOffset.UtcNow;
        var merchant = new Merchant(Guid.NewGuid(), name, description, now);
        var account = new MerchantAccount(Guid.NewGuid(), merchant.Id, userId, now);
        var merchantBranch = new MerchantBranch(
            Guid.NewGuid(), merchant.Id, branchName, resolved.Coordinates!, phone, website, resolved.AddressDisplayName, now);

        if (!await storage.TryAddOnboardingAsync(merchant, account, merchantBranch, ct))
            return Fail(MerchantError.AlreadyOnboarded, "This account has already completed onboarding.");

        // Detached copy for the response, independent of change-tracker fixup.
        var created = new Merchant(merchant.Id, merchant.Name, merchant.Description, merchant.CreatedAt);
        created.Branches.Add(merchantBranch);

        return new MerchantResult<Merchant>(created, MerchantError.None);
    }

    private static string? Validate(
        OnboardingInput input,
        out string name,
        out string? description,
        out string branchName,
        out string? phone,
        out string? website)
    {
        name = input.Name?.Trim() ?? string.Empty;
        description = NullIfBlank(input.Description);
        branchName = input.Branch?.DisplayName?.Trim() ?? string.Empty;
        phone = NullIfBlank(input.Branch?.Phone);
        website = NullIfBlank(input.Branch?.Website);

        if (name.Length == 0) return "Business name must not be blank.";
        if (name.Length > MaxNameLength) return $"Business name must not exceed {MaxNameLength} characters.";
        if (description?.Length > MaxDescriptionLength) return $"Description must not exceed {MaxDescriptionLength} characters.";
        if (input.Branch is null) return "Branch details are required.";
        if (branchName.Length == 0) return "Branch name must not be blank.";
        if (branchName.Length > MaxNameLength) return $"Branch name must not exceed {MaxNameLength} characters.";
        if (phone?.Length > MaxPhoneLength) return $"Phone must not exceed {MaxPhoneLength} characters.";

        if (website is not null &&
            (website.Length > MaxWebsiteLength ||
             !Uri.TryCreate(website, UriKind.Absolute, out var uri) ||
             uri.Scheme is not ("http" or "https")))
        {
            return "Website must be a valid http(s) URL.";
        }

        return null;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MerchantResult<Merchant> Fail(MerchantError error, string message) => new(null, error, message);
}