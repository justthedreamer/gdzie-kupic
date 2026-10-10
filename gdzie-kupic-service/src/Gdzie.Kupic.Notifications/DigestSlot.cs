namespace Gdzie.Kupic.Notifications;

using Cronos;
using Microsoft.Extensions.Logging;

internal static class DigestSlot
{
    /// <summary>
    /// The latest scheduled occurrence at or before <paramref name="now"/> (UTC): the id of the slot a run belongs to, so
    /// that a re-run or a retry of the same slot is recognised. Falls back to the current minute when none is found.
    /// </summary>
    public static DateTimeOffset Current(DateTimeOffset now, string cron, TimeZoneInfo zone)
    {
        var expression = CronExpression.Parse(cron);
        var latest = expression
            .GetOccurrences(now.AddDays(-8), now, zone, fromInclusive: true, toInclusive: true)
            .Select(o => (DateTimeOffset?)o)
            .LastOrDefault();

        return (latest ?? new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero)).ToUniversalTime();
    }

    public static TimeZoneInfo ResolveZone(string id, Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogError(ex, "Digest time zone {TimeZone} was not found; falling back to UTC", id);

            return TimeZoneInfo.Utc;
        }
    }
}
