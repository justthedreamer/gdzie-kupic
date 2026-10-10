namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Service.API.Contract.Posts;
using Gdzie.Kupic.Storage;

internal static class FeedMapping
{
    // The user model has no first name yet; shown until it does.
    public const string BuyerNamePlaceholder = "Kupuj\u0105cy";

    public static Feed.FeedItem ToItem(FeedEntry e)
    {
        var p = e.Post;

        return new Feed.FeedItem(
            p.Id, p.Title, p.Description,
            new Posts.NamedRef(p.CategoryId, p.Category.Name),
            new Posts.NamedRef(p.TagId, p.Tag.Name),
            Math.Round(e.DistanceKm, 2),
            p.RadiusKm,
            BuyerNamePlaceholder,
            p.IsUrgent, p.UrgentDeadline, p.ExpiresAt, p.Status.ToString(), p.CreatedAt,
            e.MyResponse?.ToString());
    }

    public static Feed.FeedDetail ToDetail(FeedEntry e)
    {
        var i = ToItem(e);

        return new Feed.FeedDetail(
            i.Id, i.Title, i.Description, i.Category, i.Tag, i.DistanceKm, i.BuyerRadiusKm, i.BuyerName,
            i.IsUrgent, i.UrgentDeadline, i.ExpiresAt, i.Status, i.CreatedAt, i.MyResponse, e.ThreadId);
    }
}