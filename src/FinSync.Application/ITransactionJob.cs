namespace FinSync.Application;

public interface ITransactionJob
{
    string ProviderName { get; }

    Task RunAsync(CancellationToken ct);
}
