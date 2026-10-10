namespace Gdzie.Kupic.Chat;

internal sealed record ImageType(string ContentType, string Extension)
{
    public const int HeaderLength = 12;

    public static readonly ImageType Jpeg = new("image/jpeg", "jpg");
    public static readonly ImageType Png = new("image/png", "png");
    public static readonly ImageType WebP = new("image/webp", "webp");

    /// <summary>Detects JPEG, PNG and WebP by magic bytes; null for anything else.</summary>
    public static ImageType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return Jpeg;

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return Png;

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8)) return WebP;

        return null;
    }
}