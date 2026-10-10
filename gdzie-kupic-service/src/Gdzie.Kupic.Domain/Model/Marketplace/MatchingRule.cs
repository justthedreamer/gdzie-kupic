namespace Gdzie.Kupic.Domain.Model.Marketplace;

using Gdzie.Kupic.Domain.Model.Location;

/// <summary>The rule deciding which merchants a post reaches; shared by every matching job.</summary>
public static class MatchingRule
{
    public const double ToleranceKm = 3;

    private const double EarthRadiusMeters = 6_371_008.8;

    /// <summary>Null (unlimited radius) means any distance matches.</summary>
    public static double? MaxDistanceMeters(decimal? radiusKm) =>
        radiusKm is null ? null : ((double)radiusKm.Value + ToleranceKm) * 1000;

    public static bool IsSubscriptionMatch(MerchantSubscription subscription, Guid categoryId, Guid tagId) =>
        subscription.CategoryId == categoryId && (subscription.TagId is null || subscription.TagId == tagId);

    public static bool IsWithinReach(Coordinates post, decimal? radiusKm, Coordinates branch) =>
        MaxDistanceMeters(radiusKm) is not { } max || DistanceMeters(post, branch) <= max;

    public static double DistanceMeters(Coordinates a, Coordinates b)
    {
        var lat1 = ToRadians(a.Latitude);
        var lat2 = ToRadians(b.Latitude);
        var dLat = lat2 - lat1;
        var dLon = ToRadians(b.Longitude - a.Longitude);

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
