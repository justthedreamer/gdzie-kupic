using Gdzie.Kupic.Chat;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Chat;

public class ImageTypeTests
{
    [Test]
    public void Detect_RecognisesJpegPngAndWebPByMagicBytes()
    {
        ImageType.Detect([0xFF, 0xD8, 0xFF, 0xDB]).ShouldBe(ImageType.Jpeg);
        ImageType.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]).ShouldBe(ImageType.Png);
        ImageType.Detect("RIFF\0\0\0\0WEBPVP8 "u8).ShouldBe(ImageType.WebP);
    }

    [Test]
    public void Detect_RejectsEverythingElse()
    {
        ImageType.Detect("GIF89a......"u8).ShouldBeNull();
        ImageType.Detect("<svg></svg>"u8).ShouldBeNull();
        ImageType.Detect("RIFF\0\0\0\0WAVEfmt "u8).ShouldBeNull();
        ImageType.Detect([0xFF, 0xD8]).ShouldBeNull();
        ImageType.Detect([]).ShouldBeNull();
    }

    [Test]
    public void PresignedUrl_IsSignedForThePublicEndpointAndExpires()
    {
        using var storage = new S3ObjectStorage(Options.Create(new StorageSettings
        {
            Endpoint = "http://minio:9000",
            PublicEndpoint = "http://localhost:9000",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "attachments",
        }));

        var url = new Uri(storage.GetPresignedUrl("chat/t/m.jpg", TimeSpan.FromMinutes(15)));

        url.Scheme.ShouldBe("http");
        url.Host.ShouldBe("localhost");
        url.Port.ShouldBe(9000);
        url.AbsolutePath.ShouldBe("/attachments/chat/t/m.jpg");
        url.Query.ShouldContain("X-Amz-Expires=900");
        url.Query.ShouldContain("X-Amz-Signature=");
    }
}