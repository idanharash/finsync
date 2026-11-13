using FinSync.Shared;

namespace FinSync.Tests.Shared;

public class SensitiveDataMaskerTests
{
    private const char LeftToRightEmbedding = (char)0x202A;
    private const char RightToLeftMark = (char)0x200F;

    private readonly SensitiveDataMasker _masker = ShippedSettings.CreateSensitiveDataMasker();

    private string Mask(string description, string[]? sensitiveValues = null, string[]? trustedValues = null) =>
        _masker.Mask(description, sensitiveValues ?? [], trustedValues ?? []).Value;

    [Fact]
    public void Mask_DatesTimesDecimalsAndShortNumbers_AreUnchanged()
    {
        const string description = "כספונט 109(י) | תאריך ביצוע: 11/10/2026 | שעת ביצוע: 17:24:36 | שער: 341.84";

        Assert.Equal(description, Mask(description));
    }

    [Fact]
    public void Mask_IdentifierNumbers_KeepOnlyLastTwoDigits()
    {
        Assert.Equal("ויזה כא\"ל (י) - **44", Mask("ויזה כא\"ל (י) - 7044"));
        Assert.Equal("ת.ז *******82.", Mask("ת.ז 123456782."));
        Assert.Equal("חשבון **-***-****01", Mask("חשבון 12-345-678901"));
        Assert.Equal("IBAN IL*******************99", Mask("IBAN IL620108000000099999999"));
    }

    [Fact]
    public void Mask_Email_IsRedacted()
    {
        Assert.Equal("contact: ***", Mask("contact: someone@example.com"));
    }

    [Fact]
    public void Mask_SensitiveValues_AreRedactedIgnoringCaseLongestFirstAndAcrossWhitespace()
    {
        Assert.Equal("Transfer from ***", Mask("Transfer from John Smith", ["john smith"]));
        Assert.Equal("שם מוטב: ***", Mask("שם מוטב: דנה כהן", ["דנה", "דנה כהן"]));
        Assert.Equal("שם מוטב: ***", Mask("שם מוטב: דנה   כהן", ["דנה כהן"]));
    }

    [Fact]
    public void Mask_SensitiveValueTooShortToRedactReliably_RedactsWholeDescription()
    {
        Assert.Equal(SensitiveDataMasker.Redacted, Mask("משיכה א", ["א"]));
    }

    [Fact]
    public void Mask_FormatCharactersAndExtraWhitespace_AreRemoved()
    {
        Assert.Equal("משכורת (י)", Mask($"  משכורת {LeftToRightEmbedding}(י){RightToLeftMark}  "));
    }

    [Fact]
    public void Mask_EmptyDescription_Throws()
    {
        Assert.Throws<ArgumentException>(() => Mask("   "));
        Assert.Throws<ArgumentException>(() => Mask($"{LeftToRightEmbedding}{RightToLeftMark}"));
    }

    [Fact]
    public void Mask_TrustedValues_KeepOnlyExactMatchesAndNeverOverrideSensitiveValues()
    {
        Assert.Equal(
            "ערך נקוב: 76780 | אסמכתא: ****01",
            Mask("ערך נקוב: 76780 | אסמכתא: 767801", trustedValues: ["76780"]));
        Assert.Equal("חשבון: ***", Mask("חשבון: 803480", ["803480"], ["803480"]));
    }
}
