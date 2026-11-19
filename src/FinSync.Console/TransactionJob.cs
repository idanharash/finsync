using FinSync.Domain;
using FinSync.IO.Json;
using FinSync.Mizrahi;

namespace FinSync.ConsoleHost;

public sealed class TransactionJob(IMizrahiCollector collector, MizrahiAdapter adapter, ITransactionWriter writer)
{
    public string ProviderName => nameof(TransactionResource.MizrahiBank);

    public async Task RunAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var rawTransactions = await CollectAsync(ct);

        ct.ThrowIfCancellationRequested();
        var transactions = Adapt(rawTransactions);

        ct.ThrowIfCancellationRequested();
        await SaveAsync(transactions, ct);
    }

    private async Task<IReadOnlyList<MizrahiTransaction>> CollectAsync(CancellationToken ct)
    {
        var rawTransactions = await RunStageAsync(TransactionJobStage.Collect, () => collector.CollectAsync(ct));

        return rawTransactions ?? throw Failure(TransactionJobStage.Collect, "collector returned no result");
    }

    private IReadOnlyList<Transaction> Adapt(IReadOnlyList<MizrahiTransaction> rawTransactions)
    {
        var transactions = RunStage(TransactionJobStage.Adapt, () => adapter.Convert(rawTransactions))
            ?? throw Failure(TransactionJobStage.Adapt, "adapter returned no result");

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
