namespace Gdzie.Kupic.Service.API.Contract.Account;

public sealed class Profile
{
    /// <param name="Email">Shown to the account owner only; never exposed to other users.</param>
    /// <param name="FirstName">Optional first name, the only personal name other users (e.g. Merchants) see.</param>
    public sealed record Response(string Email, string? FirstName, string Role);

    /// <param name="FirstName">The new first name; empty or null clears it.</param>
    public sealed record UpdateRequest(string? FirstName);
}
