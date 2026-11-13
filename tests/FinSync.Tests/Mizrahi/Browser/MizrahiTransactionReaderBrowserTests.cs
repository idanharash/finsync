using PuppeteerSharp;
using FinSync.Mizrahi;

namespace FinSync.Tests.Mizrahi.Browser;

[Trait("Category", "Browser")]
[Collection(ChromiumFixture.CollectionName)]
public class MizrahiTransactionReaderBrowserTests(ChromiumFixture chromium)
{
    private async Task<IReadOnlyList<MizrahiTransaction>> ReadAsync(
        SyntheticTransactionsPage syntheticPage,
        CancellationToken ct = default)
    {
        await using var page = await chromium.Browser.NewPageAsync();
        await page.SetContentAsync(syntheticPage.ToHtml());

        return await new MizrahiTransactionReader().ReadAsync(page.MainFrame, ct);
    }

    private async Task<MizrahiPageException> ReadFailureAsync(SyntheticTransactionsPage syntheticPage) =>
        await Assert.ThrowsAsync<MizrahiPageException>(() => ReadAsync(syntheticPage));

    [Fact]
    public async Task ReadAsync_SkipsGroupHeadersAndReadsTransactionData()
    {
        var transactions = await ReadAsync(new SyntheticTransactionsPage()
            .WithGroupHeader("תנועות אחרונות")
            .WithTransaction("2026-10-30T00:00:00", -500m, "סנפומט בינלאומי 032(י)", "111", "1")
            .WithGroupHeader("יתרה קודמת")
            .WithTransaction("2026-10-05T00:00:00", 19165m, "משכורת (י)", "222", "1"));

        Assert.Equal(2, transactions.Count);
        var first = transactions[0];
        Assert.Equal("2026-10-30T00:00:00", first.Date);
        Assert.Equal("-500", first.Amount);
        Assert.Equal("סנפומט בינלאומי 032(י)", first.Description);
        Assert.Equal("111", first.OriginalReference);
        Assert.Equal("1", first.TransactionNumber);
        Assert.Empty(first.Details);
        Assert.Equal("19165", transactions[1].Amount);
    }

    [Fact]
    public async Task ReadAsync_ExpandableTransaction_ReadsDetailPairs()
    {
        var transactions = await ReadAsync(new SyntheticTransactionsPage()
            .WithTransaction(
                "2026-10-14T00:00:00", -1028.94m, "קניית *דיאמונד גמישה", "333", "1",
                ("תאור נייר:", "דיאמונד 4D גמישה"),
                ("ערך נקוב:", "301")));

        var details = Assert.Single(transactions).Details;
        Assert.Equal(
            new[] { new MizrahiTransactionDetail("תאור נייר:", "דיאמונד 4D גמישה"), new MizrahiTransactionDetail("ערך נקוב:", "301") },
            details);
    }

    [Fact]
    public async Task ReadAsync_EmptyPeriodMessage_ReturnsEmpty()
    {
        Assert.Empty(await ReadAsync(new SyntheticTransactionsPage().WithEmptyPeriodMessage()));
    }

    [Fact]
    public async Task ReadAsync_NoRowsAndNoEmptyMessage_Throws()
    {
        var exception = await ReadFailureAsync(new SyntheticTransactionsPage());

        Assert.Contains("no rows", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_MoreThanOnePage_Throws()
    {
        var exception = await ReadFailureAsync(new SyntheticTransactionsPage()
            .WithTotalPages(2)
            .WithTransaction("2026-10-01T00:00:00", -1m, "תנועה", "1", "1"));

        Assert.Contains("pagination is not supported", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_GridWidgetMissing_Throws()
    {
        await ReadFailureAsync(new SyntheticTransactionsPage()
            .WithoutGridWidget()
            .WithTransaction("2026-10-01T00:00:00", -1m, "תנועה", "1", "1"));
    }

    [Fact]
    public async Task ReadAsync_UnknownRowStructure_Throws()
    {
        var exception = await ReadFailureAsync(new SyntheticTransactionsPage()
            .WithTransaction("2026-10-01T00:00:00", -1m, "תנועה", "1", "1")
            .WithUnknownRow());

        Assert.Contains("Row 2", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_UnsupportedDetailsFormat_Throws()
    {
        var exception = await ReadFailureAsync(new SyntheticTransactionsPage()
            .WithTransactionWithUnsupportedDetails("שיק 1234567"));

        Assert.Contains("unsupported format", exception.Message);
        Assert.DoesNotContain("1234567", exception.Message);
    }
}
