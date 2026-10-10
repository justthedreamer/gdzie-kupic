namespace Gdzie.Kupic.Notifications;

/// <summary>Where the web app lives; used to build the links in e-mails.</summary>
public sealed class AppLinksSettings
{
    public const string SectionName = "App";

    public string BaseUrl { get; set; } = "http://localhost:3000";

    public string Url(string path) => $"{BaseUrl.TrimEnd('/')}{path}";

    public string SettingsUrl => Url("/settings/notifications");
}
