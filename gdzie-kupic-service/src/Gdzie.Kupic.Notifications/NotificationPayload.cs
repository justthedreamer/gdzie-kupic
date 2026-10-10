namespace Gdzie.Kupic.Notifications;

using System.Text.Json;

/// <summary>
/// The Web Push message body (shared contract with the service worker). Texts are Polish and never contain the
/// content of a chat message.
/// </summary>
internal static class NotificationPayload
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Build(NotificationKind kind, Guid? postId, Guid? threadId)
    {
        var (name, title, body) = kind switch
        {
            NotificationKind.NewPost => ("newPost", "Nowa pro\u015bba w Twojej okolicy", "Kto\u015b szuka produktu, kt\u00f3ry mo\u017cesz mie\u0107. Sprawd\u017a pro\u015bb\u0119."),
            NotificationKind.MerchantResponded => ("merchantResponded", "Sprzedawca odpowiedzia\u0142", "Sprzedawca odpowiedzia\u0142 na Twoj\u0105 pro\u015bb\u0119. Zobacz szczeg\u00f3\u0142y."),
            NotificationKind.NewMessage => ("newMessage", "Nowa wiadomo\u015b\u0107", "Masz now\u0105 wiadomo\u015b\u0107 w rozmowie."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        return JsonSerializer.Serialize(new { kind = name, postId, threadId, title, body }, Json);
    }
}
