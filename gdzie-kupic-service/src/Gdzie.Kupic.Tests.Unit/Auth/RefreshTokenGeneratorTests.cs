namespace Gdzie.Kupic.Tests.Unit.Auth;

using Gdzie.Kupic.Auth;
using Shouldly;

[TestFixture]
public class RefreshTokenGeneratorTests
{
    [Test]
    public void GenerateToken_ReturnsUniqueValues_OnEachCall()
    {
        var sut = new RefreshTokenGenerator();

        var first = sut.GenerateToken();
        var second = sut.GenerateToken();

        first.ShouldNotBe(second);
    }

    [Test]
    public void Hash_ReturnsSameValue_ForSameToken()
    {
        var sut = new RefreshTokenGenerator();
        var token = sut.GenerateToken();

        var firstHash = sut.Hash(token);
        var secondHash = sut.Hash(token);

        firstHash.ShouldBe(secondHash);
    }

    [Test]
    public void Hash_ReturnsDifferentValue_ThanPlainTextToken()
    {
        var sut = new RefreshTokenGenerator();
        var token = sut.GenerateToken();

        var hash = sut.Hash(token);

        hash.ShouldNotBe(token);
    }
}
