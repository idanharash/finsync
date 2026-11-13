using System.Globalization;
using FinSync.Abstractions;
using FinSync.Domain;

namespace FinSync.Mizrahi;

public sealed class MizrahiAdapter(ITransactionMasker<MizrahiTransaction> masker) : IAdapter<MizrahiTransaction>
{
    private const string IdentityDateFormat = "yyyy-MM-dd";
    private const string IdentityAmountFormat = "0.00####";

    private static readonly string[] BankDateFormats = ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd"];

    public IReadOnlyList<Transaction> Convert(IReadOnlyList<MizrahiTransaction> rawTransactions)
    {
        ArgumentNullException.ThrowIfNull(rawTransactions);

        return rawTransactions
            .Select((raw, index) => Convert(raw, position: index + 1))
            .ToList();
    }

    private Transaction Convert(MizrahiTransaction raw, int position)
    {
        if (raw is null)
            throw InvalidField(position, "transaction");

        var date = ParseDate(raw.Date, position);
        var amount = ParseAmount(raw.Amount, position);
        var originalReference = Required(raw.OriginalReference, position, "original reference");
        var transactionNumber = Required(raw.TransactionNumber, position, "transaction number");
        Required(raw.Description, position, "description");
        if (raw.Details is null)
            throw InvalidField(position, "details");

        var id = TransactionId.FromIdentity(
            TransactionResource.MizrahiBank,
            date.ToString(IdentityDateFormat, CultureInfo.InvariantCulture),
            amount.ToString(IdentityAmountFormat, CultureInfo.InvariantCulture),
            originalReference,
            transactionNumber);

        return new Transaction(id, TransactionResource.MizrahiBank, date, amount, masker.Mask(raw));
    }

    private static DateOnly ParseDate(string? value, int position)
    {
        var parsed = DateTime.TryParseExact(
            value?.Trim(),
            BankDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dateTime);

        var date = DateOnly.FromDateTime(dateTime);
        if (!parsed || date == default)
            throw InvalidField(position, "date");

        return date;
    }

    private static decimal ParseAmount(string? value, int position)
    {
        const NumberStyles signedDecimal = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        if (!decimal.TryParse(value?.Trim(), signedDecimal, CultureInfo.InvariantCulture, out var amount))
            throw InvalidField(position, "amount");

        return amount;
    }

    private static string Required(string? value, int position, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw InvalidField(position, field) : value.Trim();

    private static FormatException InvalidField(int position, string field) =>
        new($"Mizrahi transaction {position}: {field} is missing or invalid.");
}
