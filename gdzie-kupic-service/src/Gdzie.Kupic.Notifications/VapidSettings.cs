namespace Gdzie.Kupic.Notifications;

/// <summary>VAPID identity used to sign Web Push requests (<c>Vapid__PublicKey</c>, <c>Vapid__PrivateKey</c>, <c>Vapid__Subject</c>).</summary>
public sealed class VapidSettings
{
    public const string SectionName = "Vapid";

    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }

    /// <summary>A <c>mailto:</c> or <c>https:</c> contact URI, as required by the VAPID specification.</summary>
    public string? Subject { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey) && !string.IsNullOrWhiteSpace(Subject);
}
