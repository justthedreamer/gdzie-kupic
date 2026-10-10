namespace Gdzie.Kupic.Service.API.Contract.Account;

public sealed class NotificationSettings
{
    /// <param name="EmailEnabled">Opt-in to e-mail notifications; <c>false</c> by default.</param>
    public sealed record Response(bool EmailEnabled);

    public sealed record UpdateRequest(bool EmailEnabled);
}
