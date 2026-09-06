namespace Gdzie.Kupic.Auth;

public sealed class RefreshTokenSettings
{
    public const string SectionName = "RefreshToken";

    public int LifetimeDays { get; init; } = 30;
}
