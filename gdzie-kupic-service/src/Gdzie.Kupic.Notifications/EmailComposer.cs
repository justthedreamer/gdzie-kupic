namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Storage;

/// <summary>Builds the Polish e-mails. They link to the app page and never contain chat message content.</summary>
internal static class EmailComposer
{
    public static EmailMessage ForBuyer(EmailRecipient recipient, NotificationKind kind, Guid? postId, Guid? threadId, AppLinksSettings app)
    {
        var (subject, text, link) = kind switch
        {
            NotificationKind.MerchantResponded => (
                "Sprzedawca odpowiedzia\u0142 na Twoj\u0105 pro\u015bb\u0119",
                "Sprzedawca odpowiedzia\u0142 na Twoj\u0105 pro\u015bb\u0119 w Gdzie Kupi\u0107.",
                postId is { } p ? app.Url($"/requests/{p}") : app.Url("/home")),
            NotificationKind.NewMessage => (
                "Nowa wiadomo\u015b\u0107 od sprzedawcy",
                "Masz now\u0105 wiadomo\u015b\u0107 od sprzedawcy w Gdzie Kupi\u0107.",
                threadId is { } t ? app.Url($"/chat/{t}") : app.Url("/chat")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No e-mail is sent for this kind."),
        };

        return new EmailMessage(recipient.Email, subject, Compose(recipient.FirstName, text, link, "Otw\u00f3rz w aplikacji", app));
    }

    public static EmailMessage Digest(EmailRecipient recipient, int count, AppLinksSettings app) =>
        new(recipient.Email, "Nowe pro\u015bby czekaj\u0105 na odpowied\u017a",
            Compose(recipient.FirstName, $"Liczba pr\u00f3\u015bb czekaj\u0105cych na Twoj\u0105 odpowied\u017a: {count}.", app.Url("/feed"), "Zobacz pro\u015bby", app));

    private static string Compose(string? firstName, string text, string link, string linkLabel, AppLinksSettings app) =>
        $"{(string.IsNullOrWhiteSpace(firstName) ? "Cze\u015b\u0107" : $"Cze\u015b\u0107 {firstName}")},\n\n{text}\n\n{linkLabel}: {link}\n\n" +
        $"Nie chcesz dostawa\u0107 takich wiadomo\u015bci? Wy\u0142\u0105cz powiadomienia e-mail w ustawieniach: {app.SettingsUrl}\n";
}
