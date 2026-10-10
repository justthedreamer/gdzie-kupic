using System.Collections.Concurrent;
using Gdzie.Kupic.Chat;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>In-memory stand-in for the S3 storage; shared across the test run, so tests call <see cref="Reset"/>.</summary>
public sealed class FakeObjectStorage : IObjectStorage
{
    public ConcurrentDictionary<string, (byte[] Content, string ContentType)> Objects { get; } = new();

    public bool FailOnPut { get; set; }

    public void Reset()
    {
        Objects.Clear();
        FailOnPut = false;
    }

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        if (FailOnPut) throw new IOException("Storage is down.");

        using var copy = new MemoryStream();
        content.CopyTo(copy);
        Objects[key] = (copy.ToArray(), contentType);

        return Task.CompletedTask;
    }

    public string GetPresignedUrl(string key, TimeSpan lifetime) =>
        $"https://storage.test/{key}?X-Amz-Expires={(int)lifetime.TotalSeconds}";

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        Objects.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task EnsureBucketAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<bool> IsReachableAsync(CancellationToken ct = default) => Task.FromResult(!FailOnPut);
}