namespace FinSync.Application;

public sealed class TransactionJobException(
    string providerName,
    TransactionJobStage stage,
    string reason,
    Exception? innerException = null)
    : Exception($"{providerName} transaction job failed during {stage}: {reason}.", innerException)
{
    public string ProviderName { get; } = providerName;

    public TransactionJobStage Stage { get; } = stage;
}
