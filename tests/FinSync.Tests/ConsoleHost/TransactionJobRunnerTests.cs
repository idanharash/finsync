using FinSync.Application;
using FinSync.ConsoleHost;

namespace FinSync.Tests.ConsoleHost;

public class TransactionJobRunnerTests
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private TransactionJobRunner Runner() => new(_output, _error);

    [Fact]
    public async Task RunAllAsync_FailingProvider_IsReportedAndLaterProvidersStillRun()
    {
        var failing = new StubJob("FailingBank", _ =>
            throw new TransactionJobException("FailingBank", TransactionJobStage.Collect, "page changed"));
        var healthy = new StubJob("HealthyBank");

        var failedProviders = await Runner().RunAllAsync([failing, healthy], CancellationToken.None);

        Assert.Equal(new[] { "FailingBank" }, failedProviders);
        Assert.True(healthy.Ran);
        Assert.Contains("HealthyBank: saved.", _output.ToString());
        Assert.Contains("FailingBank transaction job failed during Collect", _error.ToString());
    }

    [Fact]
    public async Task RunAllAsync_Cancelled_StopsRemainingProviders()
    {
        using var cancellation = new CancellationTokenSource();
        var first = new StubJob("FirstBank", _ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        var second = new StubJob("SecondBank");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Runner().RunAllAsync([first, second], cancellation.Token));

        Assert.False(second.Ran);
    }

    private sealed class StubJob(string providerName, Func<CancellationToken, Task>? onRun = null) : ITransactionJob
    {
        public string ProviderName => providerName;

        public bool Ran { get; private set; }

        public Task RunAsync(CancellationToken ct)
        {
            Ran = true;
            return onRun?.Invoke(ct) ?? Task.CompletedTask;
        }
    }
}
