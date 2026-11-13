using FinSync.Abstractions;
using FinSync.Domain;

namespace FinSync.Application;

public sealed class TransactionJob<TRaw>(
    ICollector<TRaw> collector,
    IAdapter<TRaw> adapter,
    ITransactionWriter writer,
    IEnumerable<IProviderRegistration> registrations)
    : ITransactionJob
{
    public string ProviderName { get; } = registrations
        .Single(registration => registration.RawType == typeof(TRaw))
        .ProviderName;

    public async Task RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var rawTransactions = await CollectAsync(ct);

        ct.ThrowIfCancellationRequested();
        var transactions = Adapt(rawTransactions);

        ct.ThrowIfCancellationRequested();
        await SaveAsync(transactions, ct);
    }

    private async Task<IReadOnlyList<TRaw>> CollectAsync(CancellationToken ct)
    {
        var rawTransactions = await RunStageAsync(TransactionJobStage.Collect, () => collector.CollectAsync(ct));

        return rawTransactions ?? throw Failure(TransactionJobStage.Collect, "collector returned no result");
    }

    private IReadOnlyList<Transaction> Adapt(IReadOnlyList<TRaw> rawTransactions)
    {
        var transactions = RunStage(TransactionJobStage.Adapt, () => adapter.Convert(rawTransactions))
            ?? throw Failure(TransactionJobStage.Adapt, "adapter returned no result");

        if (transactions.Count != rawTransactions.Count)
            throw Failure(
                TransactionJobStage.Adapt,
                $"adapter returned {transactions.Count} transactions for {rawTransactions.Count} collected transactions");

        if (transactions.Any(transaction => transaction is null))
            throw Failure(TransactionJobStage.Adapt, "adapter returned an empty transaction");

        var duplicateIdCount = transactions
            .GroupBy(transaction => transaction.Id)
            .Count(group => group.Count() > 1);

        if (duplicateIdCount > 0)
            throw Failure(TransactionJobStage.Adapt, $"{duplicateIdCount} transaction ids are not unique");

        return transactions;
    }

    private Task SaveAsync(IReadOnlyList<Transaction> transactions, CancellationToken ct) =>
        RunStageAsync(TransactionJobStage.Save, () => writer.SaveAsync(transactions, ct));

    private T RunStage<T>(TransactionJobStage stage, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (ShouldWrap(exception))
        {
            throw Wrap(stage, exception);
        }
    }

    private async Task<T> RunStageAsync<T>(TransactionJobStage stage, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (ShouldWrap(exception))
        {
            throw Wrap(stage, exception);
        }
    }

    private async Task RunStageAsync(TransactionJobStage stage, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (ShouldWrap(exception))
        {
            throw Wrap(stage, exception);
        }
    }

    private static bool ShouldWrap(Exception exception) =>
        exception is not OperationCanceledException and not TransactionJobException;

    private TransactionJobException Wrap(TransactionJobStage stage, Exception exception) =>
        new(ProviderName, stage, exception.GetType().Name, exception);

    private TransactionJobException Failure(TransactionJobStage stage, string reason) =>
        new(ProviderName, stage, reason);
}
