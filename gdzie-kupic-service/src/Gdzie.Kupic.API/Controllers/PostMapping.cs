namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Posts;

internal static class PostMapping
{
    public static Posts.PostWithCountDto ToDto(PostView view)
    {
        var p = view.Post;

        return new Posts.PostWithCountDto(
            p.Id,
            p.Title,
            p.Description,
            p.Coordinates.Latitude,
            p.Coordinates.Longitude,
            p.RadiusKm,
            new Posts.NamedRef(p.CategoryId, p.Category.Name),
            new Posts.NamedRef(p.TagId, p.Tag.Name),
            p.Status.ToString(),
            p.NotificationDispatchStatus.ToString(),
            p.IsUrgent,
            p.UrgentDeadline,
            p.ExpiresAt,
            p.IsLongLived,
            p.CreatedAt,
            view.NotifiedCount);
    }
}
