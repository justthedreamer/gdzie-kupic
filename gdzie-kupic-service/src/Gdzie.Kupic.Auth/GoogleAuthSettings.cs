namespace Gdzie.Kupic.Auth;

public sealed class GoogleAuthSettings
{
    public const string SectionName = "GoogleAuth";

    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string FrontendCallbackUrl { get; init; }
}

public static class GoogleAuthConstants
{
    /// <summary>
    /// Temporary cookie scheme used only to carry the ClaimsPrincipal from the Google
    /// authentication handler to our own callback action, right after which it is signed out.
    /// </summary>
    public const string ExternalCookieScheme = "ExternalGoogle";
}
