namespace Gdzie.Kupic.Marketplace;

using Gdzie.Kupic.Domain.Model.Marketplace;

public enum MerchantError
{
    None,
    Validation,
    AlreadyOnboarded,
    NotOnboarded,
    GeocodingFailed
}

public sealed record MerchantResult<T>(T? Value, MerchantError Error, string? Message = null)
{
    public bool IsSuccess => Error == MerchantError.None;
}

public sealed record BranchInput(
    string? DisplayName,
    string? Phone,
    string? Website,
    double? Latitude,
    double? Longitude,
    string? Address);

public sealed record OnboardingInput(string? Name, string? Description, BranchInput? Branch);

public interface IMerchantService
{
    Task<MerchantResult<Merchant>> GetMeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Creates merchant, account link and branch atomically; a user can onboard only once.</summary>
    Task<MerchantResult<Merchant>> OnboardAsync(Guid userId, OnboardingInput input, CancellationToken ct = default);
}