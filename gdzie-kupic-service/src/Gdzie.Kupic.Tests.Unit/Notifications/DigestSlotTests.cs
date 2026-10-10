using Gdzie.Kupic.Notifications;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Notifications;

public class DigestSlotTests
{
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    [Test]
    public void AfterTheScheduledTime_IsTodaysOccurrence()
    {
        var slot = DigestSlot.Current(new DateTimeOffset(2026, 10, 10, 8, 30, 0, TimeSpan.Zero), "0 9 * * *", Warsaw);

        slot.ShouldBe(new DateTimeOffset(2026, 10, 10, 7, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public void BeforeTheScheduledTime_IsYesterdaysOccurrence()
    {
        var slot = DigestSlot.Current(new DateTimeOffset(2026, 10, 10, 6, 0, 0, TimeSpan.Zero), "0 9 * * *", Warsaw);

        slot.ShouldBe(new DateTimeOffset(2026, 10, 9, 7, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public void TwiceADay_HasTwoSlotsPerDay()
    {
        var morning = DigestSlot.Current(new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero), "0 9,18 * * *", Warsaw);
        var evening = DigestSlot.Current(new DateTimeOffset(2026, 10, 10, 17, 0, 0, TimeSpan.Zero), "0 9,18 * * *", Warsaw);

        morning.ShouldBe(new DateTimeOffset(2026, 10, 10, 7, 0, 0, TimeSpan.Zero));
        evening.ShouldBe(new DateTimeOffset(2026, 10, 10, 16, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public void FollowsDaylightSaving()
    {
        var winter = DigestSlot.Current(new DateTimeOffset(2026, 12, 10, 12, 0, 0, TimeSpan.Zero), "0 9 * * *", Warsaw);

        winter.ShouldBe(new DateTimeOffset(2026, 12, 10, 8, 0, 0, TimeSpan.Zero));
    }
}
