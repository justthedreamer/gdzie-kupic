namespace Gdzie.Kupic.Service.API.Contract.Auth;

using Gdzie.Kupic.Service.API.Contract.Models;

public class MerchantSignIn
{
    public sealed record Request(string Email, string Password);

    public sealed record Response(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt,
        MerchantAccount Account);
}