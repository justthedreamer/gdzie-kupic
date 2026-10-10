namespace Gdzie.Kupic.Chat;

/// <summary>Private S3-compatible object storage (MinIO locally, AWS S3 in production).</summary>
public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Short-lived presigned GET URL; computed locally, no network call.</summary>
    string GetPresignedUrl(string key, TimeSpan lifetime);

    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Creates the bucket when it does not exist yet (development convenience).</summary>
    Task EnsureBucketAsync(CancellationToken ct = default);

    Task<bool> IsReachableAsync(CancellationToken ct = default);
}