using Microsoft.Extensions.Options;
using FinSync.ConsoleHost;
using FinSync.Domain;
using FinSync.Mizrahi;

namespace FinSync.Tests.Application;

// The job runs with the real adapter and masker; only the browser (collector) and the file system (writer) are stubbed.
public class TransactionJobTests
{
    private static readonly IReadOnlyList<MizrahiTransaction> CollectedRaw = [TestTransactions.Raw("1"), TestTransactions.Raw("2")];

    private readonly List<string> _calls = [];
    private readonly MizrahiAdapter _adapter = new(new MizrahiTransactionMasker(
        ShippedSettings.CreateSensitiveDataMasker(), Options.Create(ShippedSettings.MizrahiMasking())));
    private readonly StubCollector _collector;
    private readonly StubWriter _writer;
    private readonly TransactionJob _job;

    public TransactionJobTests()
    {
        _collector = new StubCollector(_calls) { OnCollect = _ => Task.FromResult(CollectedRaw) };
        _writer = new StubWriter(_calls);
        _job = new TransactionJob(_collector, _adapter, _writer);
    }

    [Fact]
    public async Task RunAsync_RunsCollectAdaptSaveInOrderAndHandsDataOver()
    {
        using var cancellation = new CancellationTokenSource();

        await _job.RunAsync(cancellation.Token);

        Assert.Equal(new[] { "collect", "save" }, _calls);
        Assert.Equal(_adapter.Convert(CollectedRaw).Select(transaction => transaction.Id), _writer.Received!.Select(transaction => transaction.Id));
        Assert.Equal(cancellation.Token, _collector.ReceivedToken);
        Assert.Equal(cancellation.Token, _writer.ReceivedToken);
    }

    [Fact]
    public async Task RunAsync_EmptyPeriod_SavesEmptyList()
    {
        _collector.OnCollect = _ => Task.FromResult<IReadOnlyList<MizrahiTransaction>>([]);

        await _job.RunAsync(CancellationToken.None);

        Assert.Empty(_writer.Received!);
    }

    [Fact]
    public async Task RunAsync_CollectFails_ReportsStageKeepsCauseAndStops()
    {
        var failure = new InvalidOperationException("stage failed");
        _collector.OnCollect = _ => throw failure;

        var exception = await Assert.ThrowsAsync<TransactionJobException>(() => _job.RunAsync(CancellationToken.None));

        Assert.Equal(TransactionJobStage.Collect, exception.Stage);
        Assert.Same(failure, exception.InnerException);
        Assert.Equal(new[] { "collect" }, _calls);
    }

    [Fact]
    public async Task RunAsync_AdaptFails_ReportsStageKeepsCauseAndDoesNotSave()
    {
        _collector.OnCollect = _ => Task.FromResult<IReadOnlyList<MizrahiTransaction>>([TestTransactions.Raw("1") with { Amount = "not a number" }]);

        var exception = await Assert.ThrowsAsync<TransactionJobException>(() => _job.RunAsync(CancellationToken.None));

        Assert.Equal(TransactionJobStage.Adapt, exception.Stage);
        Assert.IsType<FormatException>(exception.InnerException);
        Assert.Equal(new[] { "collect" }, _calls);
    }

    [Fact]
    public async Task RunAsync_SaveFails_ReportsStageAndKeepsCause()
    {
        var failure = new IOException("disk full");
        _writer.OnSave = (_, _) => throw failure;

        var exception = await Assert.ThrowsAsync<TransactionJobException>(() => _job.RunAsync(CancellationToken.None));

        Assert.Equal(TransactionJobStage.Save, exception.Stage);
        Assert.Same(failure, exception.InnerException);
        Assert.Equal(new[] { "collect", "save" }, _calls);
    }

    [Fact]
    public async Task RunAsync_DuplicateTransactionIds_FailBeforeSaving()
    {
        _collector.OnCollect = _ => Task.FromResult<IReadOnlyList<MizrahiTransaction>>([TestTransactions.Raw("1"), TestTransactions.Raw("1")]);

        var exception = await Assert.ThrowsAsync<TransactionJobException>(() => _job.RunAsync(CancellationToken.None));

        Assert.Equal(TransactionJobStage.Adapt, exception.Stage);
        Assert.Contains("1 transaction ids are not unique", exception.Message);
        Assert.DoesNotContain("save", _calls);
    }

    [Fact]
    public async Task RunAsync_FailureMessage_NamesProviderButNotInnerMessage()
    {
        _collector.OnCollect = _ => throw new InvalidOperationException("שם מעביר: ישראל ישראלי 123456782");

        var exception = await Assert.ThrowsAsync<TransactionJobException>(() => _job.RunAsync(CancellationToken.None));

        Assert.Contains(nameof(TransactionResource.MizrahiBank), exception.Message);
        Assert.DoesNotContain("ישראל", exception.Message);
        Assert.DoesNotContain("123456782", exception.Message);
    }

    [Fact]
    public async Task RunAsync_CancelledAfterCollect_StopsWithUnwrappedCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        _collector.OnCollect = _ =>
        {
            cancellation.Cancel();
            return Task.FromResult(CollectedRaw);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _job.RunAsync(cancellation.Token));

        Assert.Equal(new[] { "collect" }, _calls);
    }
}
