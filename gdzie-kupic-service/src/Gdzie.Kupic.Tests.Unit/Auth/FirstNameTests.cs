namespace Gdzie.Kupic.Tests.Unit.Auth;

using Gdzie.Kupic.Domain.Model.Auth;
using Shouldly;

[TestFixture]
public class FirstNameTests
{
    [TestCase("Anna", "Anna")]
    [TestCase("  Anna  ", "Anna")]
    [TestCase("Anna  Maria", "Anna Maria")]
    [TestCase("Jan-Paweł", "Jan-Paweł")]
    [TestCase("D'Arcy", "D'Arcy")]
    [TestCase("O’Neil", "O’Neil")]
    [TestCase("Żaneta", "Żaneta")]
    [TestCase("Zoë", "Zoë")]
    [TestCase("Мария", "Мария")]
    public void Normalize_AcceptsAndCleansUpNames(string input, string expected)
    {
        var (value, error) = FirstName.Normalize(input);

        error.ShouldBeNull();
        value.ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Normalize_TreatsEmptyInputAsNoName(string? input)
    {
        var (value, error) = FirstName.Normalize(input);

        value.ShouldBeNull();
        error.ShouldBeNull();
    }

    [TestCase("Anna1")]
    [TestCase("123")]
    [TestCase("Anna@example.com")]
    [TestCase("<script>")]
    [TestCase("-Anna")]
    [TestCase("'Anna")]
    [TestCase("Anna_Maria")]
    [TestCase("Anna.")]
    public void Normalize_RejectsCharactersOtherThanLettersSpacesHyphensAndApostrophes(string input)
    {
        var (value, error) = FirstName.Normalize(input);

        value.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Test]
    public void Normalize_AllowsExactlyTheMaximumLength()
    {
        var (value, error) = FirstName.Normalize(new string('a', FirstName.MaxLength));

        error.ShouldBeNull();
        value!.Length.ShouldBe(FirstName.MaxLength);
    }

    [Test]
    public void Normalize_RejectsNamesLongerThanTheMaximum()
    {
        var (value, error) = FirstName.Normalize(new string('a', FirstName.MaxLength + 1));

        value.ShouldBeNull();
        error.ShouldNotBeNull();
    }
}
