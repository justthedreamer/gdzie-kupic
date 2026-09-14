namespace Gdzie.Kupic.Tests.Unit.Auth;

using Gdzie.Kupic.Auth;
using Shouldly;

[TestFixture]
public class PasswordHasherTests
{
    [Test]
    public void Hash_ReturnsValue_ThatIsNotEqualToPlainTextPassword()
    {
        var sut = new PasswordHasher();

        var hash = sut.Hash("super-secret-password");

        hash.ShouldNotBe("super-secret-password");
        hash.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public void Verify_ReturnsTrue_WhenPasswordMatchesHash()
    {
        var sut = new PasswordHasher();
        var hash = sut.Hash("super-secret-password");

        var result = sut.Verify("super-secret-password", hash);

        result.ShouldBeTrue();
    }

    [Test]
    public void Verify_ReturnsFalse_WhenPasswordDoesNotMatchHash()
    {
        var sut = new PasswordHasher();
        var hash = sut.Hash("super-secret-password");

        var result = sut.Verify("wrong-password", hash);

        result.ShouldBeFalse();
    }
}
