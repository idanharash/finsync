using FinSync.Application;

namespace FinSync.ConsoleHost;

public sealed class TransactionJobRunner(TextWriter output, TextWriter error)
{
    private const string TrustedExceptionNamespacePrefix = "FinSync";

    public async Task<IReadOnlyList<string>> RunAllAsync(IEnumerable<ITransactionJob> jobs, CancellationToken ct)
    {
        var failedProviders = new List<string>();

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            output.WriteLine($"{job.ProviderName}: collecting transactions...");

            try
            {
                await job.RunAsync(ct);
                output.WriteLine($"{job.ProviderName}: saved.");
            }
            catch (TransactionJobException exception)
            {
                failedProviders.Add(job.ProviderName);
                error.WriteLine(Describe(exception));
            }
        }

        return failedProviders;
    }

    private static string Describe(TransactionJobException exception) =>
        exception.InnerException?.GetType().Namespace?.StartsWith(TrustedExceptionNamespacePrefix, StringComparison.Ordinal) == true
            ? $"{exception.Message} {exception.InnerException.Message}"
            : exception.Message;
}
