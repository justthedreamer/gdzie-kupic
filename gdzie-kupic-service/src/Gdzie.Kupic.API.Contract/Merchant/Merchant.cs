namespace Gdzie.Kupic.Service.API.Contract.Merchant;

public sealed class Onboarding
{
    /// <summary>Provide either <see cref="Latitude"/> + <see cref="Longitude"/> or <see cref="Address"/>.</summary>
    public sealed record BranchRequest(
        string DisplayName,
        string? Phone,
        string? Website,
        double? Latitude,
        double? Longitude,
        string? Address);

    public sealed record Request(string Name, string? Description, BranchRequest Branch);
}

public sealed record BranchResponse(
    Guid Id,
    string DisplayName,
    double Latitude,
    double Longitude,
    string? AddressDisplayName,
    string? Phone,
    string? Website);

public sealed record MerchantMeResponse(Guid MerchantId, string Name, string? Description, BranchResponse Branch);