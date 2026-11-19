namespace FinSync.Domain;

public sealed record Transaction
{
    public TransactionId Id { get; }
    public TransactionResource Resource { get; }
    public DateOnly Date { get; }
    public decimal Amount { get; }
    public MaskedDescription Description { get; }

    public Transaction(
        TransactionId id,
        TransactionResource resource,
        DateOnly date,
        decimal amount,
        MaskedDescription description)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(description);
        if (!Enum.IsDefined(resource))
            throw new ArgumentOutOfRangeException(nameof(resource), "Unsupported transaction resource.");
        if (date == default)
            throw new ArgumentException("Transaction date is required.", nameof(date));

        Id = id;
        Resource = resource;
        Date = date;
        Amount = amount;
        Description = description;
    }
}
