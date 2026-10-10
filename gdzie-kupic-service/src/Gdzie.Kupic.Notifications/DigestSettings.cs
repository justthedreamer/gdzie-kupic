namespace Gdzie.Kupic.Notifications;

/// <summary>Schedule of the merchant digest e-mail. Twice a day is just another cron expression, e.g. <c>0 9,18 * * *</c>.</summary>
public sealed class DigestSettings
{
    public const string SectionName = "Digest";

    public bool Enabled { get; set; } = true;

    public string Cron { get; set; } = "0 9 * * *";

    public string TimeZone { get; set; } = "Europe/Warsaw";
}
