namespace Gdzie.Kupic.Service.API.Contract.Auth;

public class Refresh
{
    public sealed record Request(string RefreshToken);

    public sealed record Response(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt);
}
