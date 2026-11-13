using FinSync.Domain;
using FinSync.Shared;

namespace FinSync.Tests.Domain;

public class TransactionTests
{
    private static readonly TransactionId Id = TransactionId.FromIdentity(TransactionResource.MizrahiBank, "2026-10-03", "-607.90", "1");
    private static readonly MaskedDescription Description = ShippedSettings.CreateSensitiveDataMasker().Mask("כספונט 109(י)", [], []);
    private static readonly DateOnly Date = new(2026, 10, 3);

    [Fact]
    public void Constructor_ValidValues_ExposesValuesAndKeepsDebitSign()
    {
        var transaction = new Transaction(Id, TransactionResource.MizrahiBank, Date, -607.90m, Description);

        Assert.Equal(Id, transaction.Id);
        Assert.Equal(TransactionResource.MizrahiBank, transaction.Resource);
        Assert.Equal(Date, transaction.Date);
        Assert.Equal(-607.90m, transaction.Amount);
        Assert.Equal(Description, transaction.Description);
    }

    [Fact]
    public void Constructor_InvalidState_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Transaction(Id, default, Date, 1m, Description));
        Assert.Throws<ArgumentException>(() => new Transaction(Id, TransactionResource.MizrahiBank, default, 1m, Description));
        Assert.Throws<ArgumentNullException>(() => new Transaction(null!, TransactionResource.MizrahiBank, Date, 1m, Description));
        Assert.Throws<ArgumentNullException>(() => new Transaction(Id, TransactionResource.MizrahiBank, Date, 1m, null!));
    }
}
