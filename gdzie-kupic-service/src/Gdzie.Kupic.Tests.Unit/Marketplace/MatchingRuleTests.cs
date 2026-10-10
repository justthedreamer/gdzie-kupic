using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Marketplace;

public class MatchingRuleTests
{
    private static readonly Coordinates Post = new(50.0, 19.0);

    [Test]
    public void Distance_OneDegreeOfLatitude_IsAbout111Km() =>
        MatchingRule.DistanceMeters(Post, new Coordinates(51.0, 19.0)).ShouldBe(111_195, 200);

    [TestCase(0.036, true)]
    [TestCase(0.063, true)]
    [TestCase(0.081, false)]
    public void Reach_UsesRadiusPlusThreeKmTolerance(double latitudeOffset, bool expected) =>
        MatchingRule.IsWithinReach(Post, 5m, new Coordinates(50.0 + latitudeOffset, 19.0)).ShouldBe(expected);

    [Test]
    public void Reach_UnlimitedRadius_MatchesAnyDistance() =>
        MatchingRule.IsWithinReach(Post, null, new Coordinates(-33.0, 151.0)).ShouldBeTrue();

    [Test]
    public void Subscription_CategoryLevelAndExactTagMatch_OtherTagDoesNot()
    {
        var category = Guid.NewGuid();
        var tag = Guid.NewGuid();
        MerchantSubscription Sub(Guid? t) => new(Guid.NewGuid(), Guid.NewGuid(), category, t, DateTimeOffset.UtcNow);

        MatchingRule.IsSubscriptionMatch(Sub(null), category, tag).ShouldBeTrue();
        MatchingRule.IsSubscriptionMatch(Sub(tag), category, tag).ShouldBeTrue();
        MatchingRule.IsSubscriptionMatch(Sub(Guid.NewGuid()), category, tag).ShouldBeFalse();
        MatchingRule.IsSubscriptionMatch(Sub(null), Guid.NewGuid(), tag).ShouldBeFalse();
    }
}
