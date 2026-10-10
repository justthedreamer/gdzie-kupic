namespace Gdzie.Kupic.Service.API.Contract.Merchant;

public sealed class Feed
{
    public sealed record RespondRequest(string State);

    public sealed record RespondResponse(string State, Guid? ThreadId, DateTimeOffset UpdatedAt);
}