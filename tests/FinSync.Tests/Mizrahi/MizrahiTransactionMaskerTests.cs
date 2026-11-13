using Microsoft.Extensions.Options;
using FinSync.Abstractions;
using FinSync.Domain;
using FinSync.Shared;
using FinSync.Mizrahi;

namespace FinSync.Tests.Mizrahi;

public class MizrahiTransactionMaskerTests
{
    private readonly MizrahiTransactionMasker _masker = new(ShippedSettings.CreateSensitiveDataMasker(), Options.Create(ShippedSettings.MizrahiMasking()));

    private static MizrahiTransaction Raw(string description, params MizrahiTransactionDetail[] details) =>
        new("2026-10-13T00:00:00", "55", description, "100200300", "1", details);

    private static MizrahiTransactionDetail Detail(string label, string value) => new(label, value);

    private string Mask(MizrahiTransaction raw) => _masker.Mask(raw).Value;

    [Fact]
    public void Mask_IncomingTransfer_RedactsBranchAccountAndSenderAndKeepsContext()
    {
        var raw = Raw(
            "ב.הפועלים-ביט (י)",
            Detail("בנק:", "12-בנק הפועלים בע'מ"),
            Detail("סניף:", "799-הנהלה ראשית"),
            Detail("חשבון:", "803480"),
            Detail("  שם מעביר :  ", "ישראל ישראלי"),
            Detail("מהות העברה:", "account bank to Withdrawal"),
            Detail("אסמכתא מסב:", "79720"));

        Assert.Equal(
            "ב.הפועלים-ביט (י) | בנק: 12-בנק הפועלים בע'מ | סניף: *** | חשבון: *** | שם מעביר: *** | מהות העברה: account bank to Withdrawal | אסמכתא מסב: ***20",
            Mask(raw));
    }

    [Fact]
    public void Mask_OutgoingTransfer_RedactsBeneficiaryAndKeepsDateAndTime()
    {
        var raw = Raw(
            "העברה באינטרנט (י)",
            Detail("מהות העברה:", "ניתוח"),
            Detail("תאריך ביצוע:", "11/10/2026"),
            Detail("שעת ביצוע:", "17:24:36"),
            Detail("שם מוטב:", "דנה כהן"));

        Assert.Equal(
            "העברה באינטרנט (י) | מהות העברה: ניתוח | תאריך ביצוע: 11/10/2026 | שעת ביצוע: 17:24:36 | שם מוטב: ***",
            Mask(raw));
    }

    [Fact]
    public void Mask_SecuritiesPurchase_KeepsFaceValueAndRateAndMasksReference()
    {
        var raw = Raw(
            "קניית מיטב כספ שקל כש",
            Detail("תאור נייר:", "מיטב כספית שקלית כשר"),
            Detail("סוג פעולה:", "קניה"),
            Detail("ערך נקוב:", "76780"),
            Detail("שער:", "1171.96"),
            Detail("אסמכתא מסב:", "79720"));

        Assert.Equal(
            "קניית מיטב כספ שקל כש | תאור נייר: מיטב כספית שקלית כשר | סוג פעולה: קניה | ערך נקוב: 76780 | שער: 1171.96 | אסמכתא מסב: ***20",
            Mask(raw));
    }

    [Theory]
    [InlineData("שדה חדש:", "העברה | שדה חדש: ***")]
    [InlineData("", "העברה | ***")]
    public void Mask_UnknownOrMissingLabel_RedactsValue(string label, string expected)
    {
        Assert.Equal(expected, Mask(Raw("העברה", Detail(label, "מידע לא מסווג"))));
    }

    [Fact]
    public void Mask_SensitiveDetailValueRepeatedInDescription_IsRedactedEverywhere()
    {
        Assert.Equal(
            "העברה ל*** | שם מוטב: ***",
            Mask(Raw("העברה לישראל ישראלי", Detail("שם מוטב:", "ישראל ישראלי"))));
    }

    [Fact]
    public void Mask_DelegatesComposedTextAndClassifiedValuesToSharedMasker()
    {
        var sharedMasker = new SpySensitiveDataMasker();
        var raw = Raw(
            "קניה",
            Detail("שם מעביר:", "ישראל ישראלי"),
            Detail("מהות העברה:", "החזר"),
            Detail("ערך נקוב:", "301"));

        var result = new MizrahiTransactionMasker(sharedMasker, Options.Create(ShippedSettings.MizrahiMasking())).Mask(raw);

        var call = Assert.Single(sharedMasker.Calls);
        Assert.Equal("קניה | שם מעביר: ישראל ישראלי | מהות העברה: החזר | ערך נקוב: 301", call.Description);
        Assert.Equal(new[] { "ישראל ישראלי" }, call.SensitiveValues);
        Assert.Equal(new[] { "301" }, call.TrustedValues);
        Assert.Equal(call.Result, result);
    }

    private sealed class SpySensitiveDataMasker : ISensitiveDataMasker
    {
        private readonly SensitiveDataMasker _inner = ShippedSettings.CreateSensitiveDataMasker();

        public IReadOnlyCollection<string> CommonSensitiveLabels => _inner.CommonSensitiveLabels;

        public List<(string Description, IReadOnlyCollection<string> SensitiveValues, IReadOnlyCollection<string> TrustedValues, MaskedDescription Result)> Calls { get; } = [];

        public MaskedDescription Mask(
            string description,
            IReadOnlyCollection<string> sensitiveValues,
            IReadOnlyCollection<string> trustedValues)
        {
            var result = _inner.Mask(description, sensitiveValues, trustedValues);
            Calls.Add((description, sensitiveValues, trustedValues, result));
            return result;
        }
    }
}
