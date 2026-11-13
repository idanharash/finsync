using FinSync.Domain;

namespace FinSync.IO.Json;

internal sealed record TransactionRecord(
    string Id,
    string Resource,
    DateOnly Date,
    decimal Amount,
    string Description)
{
    public static TransactionRecord From(Transaction transaction) =>
        new(
            transaction.Id.Value,
            transaction.Resource.ToString(),
            transaction.Date,
            transaction.Amount,
            transaction.Description.Value);

    public bool IsValidFor(TransactionResource resource) =>
        HasValidId()
        && Resource == resource.ToString()
        && Date != default
        && !string.IsNullOrWhiteSpace(Description);

    private bool HasValidId()
    {
        try
        {
            TransactionId.Parse(Id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
