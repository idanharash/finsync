using FinSync.Domain;

namespace FinSync.IO.Json;

public interface ITransactionWriter
{
    Task SaveAsync(IReadOnlyList<Transaction> transactions, CancellationToken ct);
}
