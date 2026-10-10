namespace Gdzie.Kupic.Chat;

using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;

internal sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly StorageSettings _settings;
    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _signer;
    private readonly Protocol _signedProtocol;

    public S3ObjectStorage(IOptions<StorageSettings> options)
    {
        _settings = options.Value;
        _client = CreateClient(_settings.Endpoint);

        var publicEndpoint = string.IsNullOrWhiteSpace(_settings.PublicEndpoint) ? _settings.Endpoint : _settings.PublicEndpoint;
        _signer = CreateClient(publicEndpoint);
        _signedProtocol = publicEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) =>
        await _client.PutObjectAsync(
            new PutObjectRequest { BucketName = _settings.BucketName, Key = key, InputStream = content, ContentType = contentType }, ct);

    public string GetPresignedUrl(string key, TimeSpan lifetime) =>
        _signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime),
            Protocol = _signedProtocol,
        });

    public async Task DeleteAsync(string key, CancellationToken ct = default) =>
        await _client.DeleteObjectAsync(_settings.BucketName, key, ct);

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        if (await AmazonS3Util.DoesS3BucketExistV2Async(_client, _settings.BucketName)) return;

        await _client.PutBucketAsync(new PutBucketRequest { BucketName = _settings.BucketName }, ct);
    }

    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        try
        {
            await _client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _settings.BucketName, MaxKeys = 1 }, ct);

            return true;
        }
        catch (Exception ex) when (ex is AmazonServiceException or HttpRequestException or IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _signer.Dispose();
    }

    private AmazonS3Client CreateClient(string endpoint)
    {
        var config = new AmazonS3Config
        {
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_settings.Region);
        }
        else
        {
            config.ServiceURL = endpoint;
            config.ForcePathStyle = true;
            config.AuthenticationRegion = _settings.Region;
        }

        return string.IsNullOrEmpty(_settings.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey), config);
    }
}