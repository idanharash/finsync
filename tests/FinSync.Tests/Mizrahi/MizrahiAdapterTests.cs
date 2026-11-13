using Microsoft.Extensions.Options;
using FinSync.Domain;
using FinSync.Shared;
using FinSync.Mizrahi;

namespace FinSync.Tests.Mizrahi;

public class MizrahiAdapterTests
{
    private readonly MizrahiAdapter _adapter = new(new MizrahiTransactionMasker(ShippedSettings.CreateSensitiveDataMasker(), Options.Create(ShippedSettings.MizrahiMasking())));

    private static MizrahiTransaction Raw(
        string date = "2026-10-09T00:00:00",
        string amount = "-607.9",
        string description = "כספונט 109(י)",
        string originalReference = "100200300",
        string transactionNumber = "1",
        params MizrahiTransactionDetail[] details) =>
        new(date, amount, description, originalReference, transactionNumber, details);

    private Transaction ConvertSingle(MizrahiTransaction raw) => Assert.Single(_adapter.Convert([raw]));

    [Fact]
    public void Convert_ValidTransaction_MapsFieldsAndMasksDescription()
    {
        var transaction = ConvertSingle(Raw(description: "ויזה כא\"ל (י) - 7044"));

        Assert.Equal(TransactionResource.MizrahiBank, transaction.Resource);
        Assert.Equal(new DateOnly(2026, 10, 9), transaction.Date);
        Assert.Equal(-607.90m, transaction.Amount);
        Assert.Equal("ויזה כא\"ל (י) - **44", transaction.Description.Value);
    }

    [Fact]
    public void Convert_ValidAmount_KeepsValueAndDebitCreditSign()
    {
        Assert.Equal(18216m, ConvertSingle(Raw(amount: "18216")).Amount);
        Assert.Equal(0m, ConvertSingle(Raw(amount: "0")).Amount);
    }

    [Fact]
    public void Convert_AmountOrDateOutsideSupportedFormat_Throws()
    {
        foreach (var amount in new[] { "1,028.94", "1e5", "₪55" })
            Assert.Contains("amount", Assert.Throws<FormatException>(() => _adapter.Convert([Raw(amount: amount)])).Message);

        foreach (var date in new[] { "09/10/2026", "0001-01-01T00:00:00" })
            Assert.Contains("date", Assert.Throws<FormatException>(() => _adapter.Convert([Raw(date: date)])).Message);
    }

    [Fact]
    public void Convert_MissingRequiredField_Throws()
    {
        Assert.Throws<FormatException>(() => _adapter.Convert([Raw(amount: "")]));
        Assert.Throws<FormatException>(() => _adapter.Convert([Raw(description: " ")]));
        Assert.Throws<FormatException>(() => _adapter.Convert([Raw(originalReference: "")]));
        Assert.Throws<FormatException>(() => _adapter.Convert([Raw(transactionNumber: "")]));
        Assert.Throws<FormatException>(() => _adapter.Convert([new MizrahiTransaction("2026-10-09", "-1", "משכורת", "1", "1", null!)]));
        Assert.Throws<FormatException>(() => _adapter.Convert([Raw(), null!]));
    }

    [Fact]
    public void Convert_InvalidRow_ReportsPositionWithoutRawValue()
    {
        var exception = Assert.Throws<FormatException>(() => _adapter.Convert([Raw(), Raw(amount: "ישראל 123456782")]));

        Assert.Contains("transaction 2", exception.Message);
        Assert.DoesNotContain("ישראל", exception.Message);
        Assert.DoesNotContain("123456782", exception.Message);
    }

    [Fact]
    public void Convert_Id_IsStableAcrossAmountFormattingAndDescriptionChanges()
    {
        var baseline = ConvertSingle(Raw()).Id;

        Assert.Equal(baseline, ConvertSingle(Raw()).Id);
        Assert.Equal(baseline, ConvertSingle(Raw(amount: "-607.90")).Id);
        Assert.Equal(baseline, ConvertSingle(Raw(description: "כספונט 109", details: [new("מהות העברה", "משיכה")])).Id);
    }

    [Fact]
    public void Convert_EachIdentityField_ChangesId()
    {
        var baseline = ConvertSingle(Raw()).Id;

        Assert.NotEqual(baseline, ConvertSingle(Raw(date: "2026-10-10T00:00:00")).Id);
        Assert.NotEqual(baseline, ConvertSingle(Raw(amount: "-607.8")).Id);
        Assert.NotEqual(baseline, ConvertSingle(Raw(originalReference: "100200301")).Id);
        Assert.NotEqual(baseline, ConvertSingle(Raw(transactionNumber: "2")).Id);
    }
}
