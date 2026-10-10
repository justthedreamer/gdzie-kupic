using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Chat;
using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ChatDto = Gdzie.Kupic.Service.API.Contract.Chat.Chat;

namespace Gdzie.Kupic.Tests.Integration;

public class ChatAttachmentTests : IntegrationTestBase
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
    private static readonly byte[] WebP = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 4, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P'];
    private static readonly byte[] Gif = "GIF89a\x01\0\x01\0\0\0\0;"u8.ToArray();
    private static readonly byte[] Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray();

    private Guid _buyerId;
    private Guid _threadId;
    private string _buyerToken = null!;
    private string _merchantToken = null!;
    private FakeObjectStorage _storage = null!;

    [SetUp]
    public async Task Seed()
    {
        _storage = IntegrationTestSetup.Factory.Services.GetRequiredService<FakeObjectStorage>();
        _storage.Reset();

        var category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        var tag = new Tag(Guid.NewGuid(), category.Id, "Microphones", false, DateTimeOffset.UtcNow);
        _buyerId = await AuthenticateAsync(Role.Buyer);
        _buyerToken = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var merchantUserId = await AuthenticateAsync(Role.Merchant);
        _merchantToken = Client.DefaultRequestHeaders.Authorization!.Parameter!;

        var merchantId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        _threadId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Categories.Add(category);
            db.Tags.Add(tag);
            db.Merchants.Add(new Merchant(merchantId, "Music Shop", null, DateTimeOffset.UtcNow));
            db.MerchantAccounts.Add(new MerchantAccount(Guid.NewGuid(), merchantId, merchantUserId, DateTimeOffset.UtcNow));
            db.Posts.Add(new Post(postId, _buyerId, new Coordinates(50, 19), 5m, category.Id, tag.Id, "Need a mic", null,
                null, DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow.AddHours(-1)));
            db.ChatThreads.Add(new ChatThread(_threadId, postId, merchantId, false, DateTimeOffset.UtcNow.AddHours(-1)));
            await db.SaveChangesAsync();
        });
    }

    private void As(string token) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private Task<HttpResponseMessage> SendAsync(string? body, byte[]? image, string? contentType = "image/jpeg", Guid? threadId = null)
    {
        var form = new MultipartFormDataContent();
        if (body is not null) form.Add(new StringContent(body), "body");
        if (image is not null)
        {
            var file = new ByteArrayContent(image);
            if (contentType is not null) file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            form.Add(file, "image", "photo.bin");
        }

        return Client.PostAsync($"/api/chat/threads/{threadId ?? _threadId}/messages", form);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("code").GetString();
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<int> MessageCountAsync()
    {
        var count = 0;
        await WithDbAsync(async db => count = await db.ChatMessages.CountAsync());
        return count;
    }

    [TestCase("image/jpeg", "jpg")]
    [TestCase("image/png", "png")]
    [TestCase("image/webp", "webp")]
    public async Task Upload_AcceptedTypes_StoreObjectUnderDocumentedKey(string contentType, string extension)
    {
        var bytes = extension switch { "jpg" => Jpeg, "png" => Png, _ => WebP };
        As(_buyerToken);

        var response = await SendAsync("look", bytes, contentType);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var message = (await ReadAsAsync<ChatDto.Message>(response))!;
        message.Body.ShouldBe("look");
        var key = $"chat/{_threadId}/{message.Id}.{extension}";
        _storage.Objects.Keys.ShouldBe([key]);
        _storage.Objects[key].Content.ShouldBe(bytes);
        _storage.Objects[key].ContentType.ShouldBe(contentType);
        message.AttachmentUrl.ShouldBe($"https://storage.test/{key}?X-Amz-Expires=900");

        await WithDbAsync(async db => (await db.ChatMessages.SingleAsync()).AttachmentKey.ShouldBe(key));
    }

    [Test]
    public async Task Upload_ImageOnly_IsAllowed_AndTextOnlyStillHasNoUrl()
    {
        As(_merchantToken);

        var imageOnly = (await ReadAsAsync<ChatDto.Message>(await SendAsync(null, Png, "image/png")))!;
        var textOnly = (await ReadAsAsync<ChatDto.Message>(await SendAsync("hi", null)))!;

        imageOnly.Body.ShouldBeNull();
        imageOnly.AttachmentUrl.ShouldNotBeNull();
        textOnly.AttachmentUrl.ShouldBeNull();
    }

    [Test]
    public async Task Upload_WithoutTextAndImage_IsRejected()
    {
        As(_buyerToken);

        (await SendAsync(null, null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync("   ", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync("", []!, "image/jpeg")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task History_ReturnsPresignedUrlsToBothParticipants()
    {
        As(_buyerToken);
        var sent = (await ReadAsAsync<ChatDto.Message>(await SendAsync(null, Jpeg)))!;

        foreach (var token in new[] { _buyerToken, _merchantToken })
        {
            As(token);
            var page = (await ReadAsAsync<ChatDto.MessagePage>(await Client.GetAsync($"/api/chat/threads/{_threadId}/messages")))!;
            page.Items.Single().AttachmentUrl.ShouldBe(sent.AttachmentUrl);
        }
    }

    [TestCase("gif", "image/gif")]
    [TestCase("svg", "image/svg+xml")]
    [TestCase("gif", "image/jpeg")]
    [TestCase("png", "image/jpeg")]
    [TestCase("jpeg", "application/pdf")]
    [TestCase("jpeg", null)]
    [TestCase("garbage", "image/png")]
    public async Task Upload_WrongOrMismatchedType_Returns415AndStoresNothing(string content, string? declared)
    {
        var bytes = content switch
        {
            "gif" => Gif,
            "svg" => Svg,
            "png" => Png,
            "jpeg" => Jpeg,
            _ => "not an image at all"u8.ToArray(),
        };
        As(_buyerToken);

        var response = await SendAsync("hello", bytes, declared);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        (await CodeAsync(response)).ShouldBe("unsupported_attachment_type");
        _storage.Objects.ShouldBeEmpty();
        (await MessageCountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Upload_AboveTheLimit_Returns413_AndTheLimitIsInclusive()
    {
        const int limit = 5 * 1024 * 1024;
        As(_buyerToken);

        var atLimit = Jpeg.Concat(new byte[limit - Jpeg.Length]).ToArray();
        (await SendAsync("ok", atLimit)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var tooBig = Jpeg.Concat(new byte[limit - Jpeg.Length + 1]).ToArray();
        var response = await SendAsync("too big", tooBig);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await CodeAsync(response)).ShouldBe("attachment_too_large");
        _storage.Objects.Count.ShouldBe(1);
        (await MessageCountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Upload_WhenStoringFails_NoMessageIsCreated()
    {
        _storage.FailOnPut = true;
        As(_buyerToken);

        // The in-process test server surfaces the unhandled storage failure instead of rendering a 500.
        await Should.ThrowAsync<IOException>(() => SendAsync("hello", Jpeg));

        (await MessageCountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Upload_ByNonParticipant_ReturnsNotFoundAndStoresNothing()
    {
        await AuthenticateAsync(Role.Buyer);

        (await SendAsync("hello", Jpeg)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        _storage.Objects.ShouldBeEmpty();
        (await MessageCountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Upload_ToLockedThread_ReturnsForbiddenBeforeStoring()
    {
        await WithDbAsync(async db =>
        {
            (await db.ChatThreads.SingleAsync()).IsLocked = true;
            await db.SaveChangesAsync();
        });
        As(_buyerToken);

        var response = await SendAsync("hello", Jpeg);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeAsync(response)).ShouldBe("thread_locked");
        _storage.Objects.ShouldBeEmpty();
    }

    [Test]
    public async Task ReadyHealthCheck_ReflectsBucketReachability()
    {
        (await Client.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);

        _storage.FailOnPut = true;
        (await Client.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}