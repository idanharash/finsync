using FinSync.Domain;
using FinSync.IO.Json;
using FinSync.Mizrahi;

namespace FinSync.Tests.Application;

public static class TestTransactions
{
    /// <summary>A valid raw bank row; the transaction number keeps rows distinct.</summary>
    public static MizrahiTransaction Raw(string transactionNumber) =>
        new("2026-10-09T00:00:00", "-607.9", "כספונט", "100200300", transactionNumber, []);
}

public sealed class StubCollector(List<string> calls) : IMizrahiCollector
{
    public Func<CancellationToken, Task<IReadOnlyList<MizrahiTransaction>>> OnCollect { get; set; } =
        _ => Task.FromResult<IReadOnlyList<MizrahiTransaction>>([]);

    public CancellationToken ReceivedToken { get; private set; }

    public Task<IReadOnlyList<MizrahiTransaction>> CollectAsync(CancellationToken ct)
    {
        calls.Add("collect");
        ReceivedToken = ct;
        return OnCollect(ct);
    }
}

public sealed class StubWriter(List<string> calls) : ITransactionWriter
{
    public Func<IReadOnlyList<Transaction>, CancellationToken, Task> OnSave { get; set; } = (_, _) => Task.CompletedTask;

    public IReadOnlyList<Transaction>? Received { get; private set; }

    public CancellationToken ReceivedToken { get; private set; }

    public Task SaveAsync(IReadOnlyList<Transaction> transactions, CancellationToken ct)
    {
        calls.Add("save");
        Received = transactions;
        ReceivedToken = ct;
        return OnSave(transactions, ct);
    }
}
