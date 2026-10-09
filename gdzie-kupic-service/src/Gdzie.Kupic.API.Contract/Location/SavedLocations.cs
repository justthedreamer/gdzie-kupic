namespace Gdzie.Kupic.Service.API.Contract.Location;

public sealed class SavedLocations
{
    /// <summary>Provide either <see cref="Latitude"/> + <see cref="Longitude"/> or <see cref="Address"/>.</summary>
    public sealed record CreateRequest(string DisplayName, double? Latitude, double? Longitude, string? Address);

    public sealed record Response(Guid Id, string DisplayName, double Latitude, double Longitude, DateTimeOffset CreatedAt);
}