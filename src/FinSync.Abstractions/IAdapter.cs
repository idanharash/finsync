using FinSync.Domain;

namespace FinSync.Abstractions;

public interface IAdapter<TRaw>
{
    IReadOnlyList<Transaction> Convert(IReadOnlyList<TRaw> rawTransactions);
}
