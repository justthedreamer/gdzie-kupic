namespace Gdzie.Kupic.Domain.Model.Auth;

using System.Text.RegularExpressions;

/// <summary>
/// Rules for a user's optional first name: the only personal name ever shown to other users
/// (e.g. a Merchant sees the Buyer's first name, never the surname or the e-mail address).
/// </summary>
public static partial class FirstName
{
    public const int MaxLength = 50;

    /// <summary>
    /// Trims and collapses whitespace. Empty input means "no name" and yields <c>(null, null)</c>.
    /// Letters (any script) may be separated by single spaces, hyphens or apostrophes.
    /// </summary>
    /// <returns>The normalized name (or null) and a validation error (or null).</returns>
    public static (string? Value, string? Error) Normalize(string? input)
    {
        var value = Whitespace().Replace(input?.Trim() ?? string.Empty, " ");

        if (value.Length == 0) return (null, null);

        if (value.Length > MaxLength) return (null, $"First name must not exceed {MaxLength} characters.");

        if (!Allowed().IsMatch(value))
            return (null, "First name may contain only letters, spaces, hyphens and apostrophes.");

        return (value, null);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^\p{L}[\p{L}\p{M} '’\-]*$")]
    private static partial Regex Allowed();
}
