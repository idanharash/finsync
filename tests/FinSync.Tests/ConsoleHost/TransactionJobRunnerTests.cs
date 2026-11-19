using Microsoft.Extensions.Options;
using FinSync.ConsoleHost;
using FinSync.Mizrahi;
using FinSync.Tests.Application;

namespace FinSync.Tests.ConsoleHost;

public class TransactionJobRunnerTests
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly List<string> _calls = [];

    private TransactionJobRunner Runner() => new(_output, _error);

    private TransactionJob Job(Func<CancellationToken, Task<IReadOnlyList<MizrahiTransaction>>> onCollect) =>
        new(
            new StubCollector(_calls) { OnCollect = onCollect },
            new MizrahiAdapter(new MizrahiTransactionMasker(
                ShippedSettings.CreateSensitiveDataMasker(), Options.Create(ShippedSettings.MizrahiMasking()))),
            new StubWriter(_calls));

    [Fact]
    public async Task RunAsync_SuccessfulJob_ReportsSaved()
    {
        var succeeded = await Runner().RunAsync(Job(_ => Task.FromResult<IReadOnlyList<MizrahiTransaction>>([])), CancellationToken.None);

        Assert.True(succeeded);
        Assert.Contains("MizrahiBank: saved.", _output.ToString());
    }

    [Fact]
    public async Task RunAsync_FailingJob_IsReportedWithTheStage()
    {
        var succeeded = await Runner().RunAsync(Job(_ => throw new MizrahiPageException("page changed")), CancellationToken.None);

        Assert.False(succeeded);
        Assert.Contains("MizrahiBank transaction job failed during Collect", _error.ToString());
        Assert.Contains("page changed", _error.ToString()); // our own exception: its message is safe to show
    }

    [Fact]
    public async Task RunAsync_ForeignException_MessageIsNotShown()
    {
        var succeeded = await Runner().RunAsync(Job(_ => throw new InvalidOperationException("ישראל ישראלי 123456782")), CancellationToken.None);

        Assert.False(succeeded);
        Assert.DoesNotContain("123456782", _error.ToString());
    }

    [Fact]
    public async Task RunAsync_Cancelled_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var job = Job(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Runner().RunAsync(job, cancellation.Token));

        Assert.DoesNotContain("save", _calls);
    }
}
