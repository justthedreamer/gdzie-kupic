namespace Gdzie.Kupic.Service.API.Contract.Auth;

public class MerchantSignIn
{
    public sealed record Request(string Email, string Password);

    public sealed record Response(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt,
        MerchantAccount Account);
}