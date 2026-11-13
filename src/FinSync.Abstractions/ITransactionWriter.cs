using FinSync.Domain;

namespace FinSync.Abstractions;

public interface ITransactionWriter
{
    Task SaveAsync(IReadOnlyList<Transaction> transactions, CancellationToken ct);
}
