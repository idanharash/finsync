using FinSync.Domain;

namespace FinSync.Tests.Domain;

public class TransactionIdTests
{
    private static TransactionId MizrahiId(string transactionNumber) =>
        TransactionId.FromIdentity(TransactionResource.MizrahiBank, "2026-10-09", "-607.90", "123456789", transactionNumber);

    [Fact]
    public void FromIdentity_IsDeterministicLowercaseSha256WithoutReadableParts()
    {
        var id = MizrahiId("1");

        Assert.Equal(id, MizrahiId("1"));
        Assert.Matches("^[0-9a-f]{64}$", id.Value);
        Assert.DoesNotContain("123456789", id.Value);
        Assert.Equal(id, TransactionId.Parse(id.Value));
    }

    [Fact]
    public void FromIdentity_AnyIdentityDifference_ProducesDifferentId()
    {
        Assert.NotEqual(MizrahiId("1"), MizrahiId("2"));
        Assert.NotEqual(
            TransactionId.FromIdentity(TransactionResource.MizrahiBank, "2026-10-09", "-607.90"),
            TransactionId.FromIdentity(TransactionResource.Cal, "2026-10-09", "-607.90"));
        Assert.NotEqual(
            TransactionId.FromIdentity(TransactionResource.MizrahiBank, "ab", "c"),
            TransactionId.FromIdentity(TransactionResource.MizrahiBank, "a", "bc"));
    }

    [Fact]
    public void FromIdentity_InvalidIdentity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TransactionId.FromIdentity(default, "a"));
        Assert.Throws<ArgumentException>(() => TransactionId.FromIdentity(TransactionResource.MizrahiBank));
        Assert.Throws<ArgumentException>(() => TransactionId.FromIdentity(TransactionResource.MizrahiBank, "2026-10-09", " "));
    }

    [Fact]
    public void Parse_InvalidFormat_Throws()
    {
        Assert.Throws<FormatException>(() => TransactionId.Parse(null!));
        Assert.Throws<FormatException>(() => TransactionId.Parse("abc"));
        Assert.Throws<FormatException>(() => TransactionId.Parse(MizrahiId("1").Value.ToUpperInvariant()));
    }
}
