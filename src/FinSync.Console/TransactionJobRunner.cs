namespace FinSync.ConsoleHost;

public sealed class TransactionJobRunner(TextWriter output, TextWriter error)
{
    private const string TrustedExceptionNamespacePrefix = "FinSync";

    /// <summary>Runs the job and reports the outcome. Returns false when the job failed; cancellation propagates.</summary>
    public async Task<bool> RunAsync(TransactionJob job, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        output.WriteLine($"{job.ProviderName}: collecting transactions...");

        try
        {
            await job.RunAsync(ct);
            output.WriteLine($"{job.ProviderName}: saved.");
            return true;
        }
        catch (TransactionJobException exception)
        {
            error.WriteLine(Describe(exception));
            return false;
        }
    }

    private static string Describe(TransactionJobException exception) =>
        exception.InnerException?.GetType().Namespace?.StartsWith(TrustedExceptionNamespacePrefix, StringComparison.Ordinal) == true
            ? $"{exception.Message} {exception.InnerException.Message}"
            : exception.Message;
}
