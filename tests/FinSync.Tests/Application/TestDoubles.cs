using FinSync.Abstractions;
using FinSync.Domain;
using FinSync.Shared;

namespace FinSync.Tests.Application;

public sealed record FakeRaw(string Value);

public sealed record OtherRaw(string Value);

public static class TestTransactions
{
    private static readonly SensitiveDataMasker Masker = ShippedSettings.CreateSensitiveDataMasker();

    public static Transaction Create(string seed) =>
        new(
            TransactionId.FromIdentity(TransactionResource.MizrahiBank, seed),
            TransactionResource.MizrahiBank,
            new DateOnly(2026, 10, 1),
            1m,
            Masker.Mask($"תנועה {seed}", [], []));

    public static IReadOnlyList<Transaction> ConvertAll(IReadOnlyList<FakeRaw> rawTransactions) =>
        rawTransactions.Select(raw => Create(raw.Value)).ToList();
}

public sealed class StubCollector(List<string> calls) : ICollector<FakeRaw>
{
    public Func<CancellationToken, Task<IReadOnlyList<FakeRaw>>> OnCollect { get; set; } =
        _ => Task.FromResult<IReadOnlyList<FakeRaw>>([]);

    public CancellationToken ReceivedToken { get; private set; }

    public Task<IReadOnlyList<FakeRaw>> CollectAsync(CancellationToken ct)
    {
        calls.Add("collect");
        ReceivedToken = ct;
        return OnCollect(ct);
    }
}

public sealed class StubAdapter(List<string> calls) : IAdapter<FakeRaw>
{
    public Func<IReadOnlyList<FakeRaw>, IReadOnlyList<Transaction>> OnConvert { get; set; } = TestTransactions.ConvertAll;

    public IReadOnlyList<FakeRaw>? Received { get; private set; }

    public IReadOnlyList<Transaction> Convert(IReadOnlyList<FakeRaw> rawTransactions)
    {
        calls.Add("adapt");
        Received = rawTransactions;
        return OnConvert(rawTransactions);
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

public sealed class FakeCollector : ICollector<FakeRaw>
{
    public Task<IReadOnlyList<FakeRaw>> CollectAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FakeRaw>>([]);
}

public sealed class FakeMasker(ISensitiveDataMasker sensitiveDataMasker) : ITransactionMasker<FakeRaw>
{
    public MaskedDescription Mask(FakeRaw rawTransaction) => sensitiveDataMasker.Mask(rawTransaction.Value, [], []);
}

public sealed class AlternativeFakeMasker(ISensitiveDataMasker sensitiveDataMasker) : ITransactionMasker<FakeRaw>
{
    public MaskedDescription Mask(FakeRaw rawTransaction) => sensitiveDataMasker.Mask(rawTransaction.Value, [], []);
}

public sealed class FakeAdapter(ITransactionMasker<FakeRaw> masker) : IAdapter<FakeRaw>
{
    public IReadOnlyList<Transaction> Convert(IReadOnlyList<FakeRaw> rawTransactions) =>
        rawTransactions
            .Select(raw => new Transaction(
                TransactionId.FromIdentity(TransactionResource.MizrahiBank, raw.Value),
                TransactionResource.MizrahiBank,
                new DateOnly(2026, 10, 1),
                1m,
                masker.Mask(raw)))
            .ToList();
}

public sealed class OtherCollector : ICollector<OtherRaw>
{
    public Task<IReadOnlyList<OtherRaw>> CollectAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OtherRaw>>([]);
}

public sealed class OtherMasker(ISensitiveDataMasker sensitiveDataMasker) : ITransactionMasker<OtherRaw>
{
    public MaskedDescription Mask(OtherRaw rawTransaction) => sensitiveDataMasker.Mask(rawTransaction.Value, [], []);
}

public sealed class OtherAdapter(ITransactionMasker<OtherRaw> masker) : IAdapter<OtherRaw>
{
    public IReadOnlyList<Transaction> Convert(IReadOnlyList<OtherRaw> rawTransactions) =>
        rawTransactions
            .Select(raw => new Transaction(
                TransactionId.FromIdentity(TransactionResource.Cal, raw.Value),
                TransactionResource.Cal,
                new DateOnly(2026, 10, 1),
                1m,
                masker.Mask(raw)))
            .ToList();
}

public sealed class FakeWriter : ITransactionWriter
{
    public Task SaveAsync(IReadOnlyList<Transaction> transactions, CancellationToken ct) => Task.CompletedTask;
}
