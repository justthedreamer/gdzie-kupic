namespace Gdzie.Kupic.Tests.Unit.Auth;

using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Shouldly;

[TestFixture]
public class AccountStatusCacheTests
{
    [Test]
    public async Task IsBannedAsync_ReturnsStorageResult_WhenNotCached()
    {
        var userId = Guid.NewGuid();
        var authStorageMock = new Mock<IAuthStorage>();
        authStorageMock.Setup(s => s.IsUserBannedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new AccountStatusCache(authStorageMock.Object, new MemoryCache(new MemoryCacheOptions()));

        var result = await sut.IsBannedAsync(userId);

        result.ShouldBeTrue();
        authStorageMock.Verify(s => s.IsUserBannedAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task IsBannedAsync_DoesNotHitStorageAgain_OnSubsequentCallWithinTtl()
    {
        var userId = Guid.NewGuid();
        var authStorageMock = new Mock<IAuthStorage>();
        authStorageMock.Setup(s => s.IsUserBannedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new AccountStatusCache(authStorageMock.Object, new MemoryCache(new MemoryCacheOptions()));

        var first = await sut.IsBannedAsync(userId);
        var second = await sut.IsBannedAsync(userId);

        first.ShouldBeFalse();
        second.ShouldBeFalse();
        authStorageMock.Verify(s => s.IsUserBannedAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
