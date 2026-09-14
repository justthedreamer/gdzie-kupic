namespace Gdzie.Kupic.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public required string Secret { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public int AccessTokenLifetimeDays { get; init; } = 7;
}
