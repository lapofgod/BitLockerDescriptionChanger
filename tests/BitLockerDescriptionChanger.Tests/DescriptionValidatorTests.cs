using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.Tests;

[TestClass]
public sealed class DescriptionValidatorTests
{
    [TestMethod]
    public void AcceptsPrintableAsciiUnicodeAndMaximumLength()
    {
        Assert.IsNull(DescriptionValidator.Validate("Archive 2026"));
        Assert.IsNull(DescriptionValidator.Validate("Caf\u00E9 archive \U0001F510"));
        Assert.IsNull(DescriptionValidator.Validate(new string('A', DescriptionValidator.MaximumUtf16CodeUnits)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("line\nbreak")]
    [DataRow("tab\tcharacter")]
    [DataRow("embedded\0null")]
    public void RejectsEmptyWhitespaceAndControlCharacters(string candidate)
    {
        Assert.IsNotNull(DescriptionValidator.Validate(candidate));
    }

    [TestMethod]
    public void RejectsOverLimitUnpairedSurrogatesAndUnchangedText()
    {
        Assert.IsNotNull(DescriptionValidator.Validate(new string('A', DescriptionValidator.MaximumUtf16CodeUnits + 1)));
        Assert.IsNotNull(DescriptionValidator.Validate("broken\uD800"));
        Assert.IsNotNull(DescriptionValidator.Validate("same", "same"));
    }

    [TestMethod]
    public void CountsSupplementaryCharactersAsTwoUtf16CodeUnits()
    {
        var accepted = string.Concat(Enumerable.Repeat("\U0001F510", 63)) + "A";
        var rejected = accepted + "B";
        Assert.AreEqual(127, accepted.Length);
        Assert.IsNull(DescriptionValidator.Validate(accepted));
        Assert.IsNotNull(DescriptionValidator.Validate(rejected));
    }

    [TestMethod]
    public void RestoreValidationAllowsAnArchivedBlankDescriptionButRejectsUnchanged()
    {
        Assert.IsNull(DescriptionValidator.ValidateRestore(string.Empty, "Current"));
        Assert.IsNull(DescriptionValidator.ValidateRestore("   ", "Current"));
        Assert.IsNotNull(DescriptionValidator.ValidateRestore("Current", "Current"));
    }
}
