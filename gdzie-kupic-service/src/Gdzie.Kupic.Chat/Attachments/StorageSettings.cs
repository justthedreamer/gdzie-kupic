namespace Gdzie.Kupic.Chat;

public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>Service URL used by the API itself; empty means AWS.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Host that browsers use to reach the storage; presigned URLs are signed for it.
    /// Falls back to <see cref="Endpoint"/> (they differ inside Docker: http://minio:9000 vs http://localhost:9000).
    /// </summary>
    public string PublicEndpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "attachments";
    public string Region { get; set; } = "us-east-1";

    /// <summary>Creates the bucket on startup; meant for development.</summary>
    public bool AutoCreateBucket { get; set; }
}

public sealed class ChatSettings
{
    public const string SectionName = "Chat";

    public long MaxAttachmentBytes { get; set; } = 5 * 1024 * 1024;
    public int AttachmentUrlLifetimeMinutes { get; set; } = 15;
}