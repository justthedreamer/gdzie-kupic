namespace Gdzie.Kupic.Service.API.Contract.Merchant;

public sealed class Subscriptions
{
    public sealed record Request(Guid CategoryId, Guid? TagId);

    public sealed record Response(Guid Id, Guid CategoryId, Guid? TagId);
}