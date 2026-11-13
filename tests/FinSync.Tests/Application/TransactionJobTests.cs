using FinSync.Application;
using FinSync.Domain;
using FinSync.Registration;

namespace FinSync.Tests.Application;

public class TransactionJobTests
{
    private const string ProviderName = "FakeProvider";

    private static readonly IReadOnlyList<FakeRaw> CollectedRaw = [new("a"), new("b")];

    private readonly List<string> _calls = [];
    private readonly StubCollector _collector;
    private readonly StubAdapter _adapter;
    private readonly StubWriter _writer;
    private readonly TransactionJob<FakeRaw> _job;

    public TransactionJobTests()
    {
        _collector = new StubCollector(_calls) { OnCollect = _ => Task.FromResult(CollectedRaw) };
        _adapter = new StubAdapter(_calls);
        _writer = new StubWriter(_calls);
        _job = new TransactionJob<FakeRaw>(_collector, _adapter, _writer, [new ProviderRegistration<FakeRaw>(ProviderName)]);
    }

    [Fact]
    public async Task RunAsync_RunsCollectAdaptSaveInOrderAndHandsDataOver()
    {
        IReadOnlyList<Transaction> adapted = [TestTransactions.Create("a"), TestTransactions.Create("b")];
        _adapter.OnConvert = _ => adapted;
        using var cancellation = new CancellationTokenSource();

        await _job.RunAsync(cancellation.Token);

        Assert.Equal(new[] { "collect", "adapt", "save" }, _calls);
        Assert.Same(CollectedRaw, _adapter.Received);
        Assert.Same(adapted, _writer.Received);
        Assert.Equal(cancellation.Token, _collector.ReceivedToken);
        Assert.Equal(cancellation.Token, _writer.ReceivedToken);
    }

    [Fact]
    public async Task RunAsync_EmptyPeriod_SavesEmptyList()
    {
        _collector.OnCollect = _ => Task.FromResult<IReadOnlyList<FakeRaw>>([]);

        await _job.RunAsync(CancellationToken.None);

        Assert.Empty(_writer.Received!);
    }

    [Theory]
    [InlineData(TransactionJobStage.Collect, "collect")]
    [InlineData(TransactionJobStage.Adapt, "collect,adapt")]
    [InlineData(TransactionJobStage.Save, "collect,adapt,save")]
    public async Task RunAsync_StageFails_ReportsStageKeepsCauseAndStops(TransactionJobStage failingStage, string expectedCalls)
    {
        var failure = new InvalidOperationException("stage failed");
        switch (failingStage)
        {
            case TransactionJobStage.Collect: _collector.OnCollect = _ => throw failure; break;
            case TransactionJobStage.Adapt: _adapter.OnConvert = _ => throw failure; break;
            case TransactionJobStage.Save: _writer.OnSave = (_, _) => throw failure; break;
        }

        var exception = await Assert.ThrowsAsync<TransactionJobException>(() => _job.RunAsync(CancellationToken.None));

        Assert.Equal(failingStage, exception.Stage);
        Assert.Same(failure, exception.InnerException);
        Assert.Equal(expectedCalls.Split(','), _calls);
    }

    [Theory]
    [InlineData("lost transaction")]
    [InlineData("duplicate id")]
    [InlineData("null transaction")]
    public async Task RunAsync_AdapterResultViolatesInvariant_FailsBeforeSaving(string violation)
    {
        IReadOnlyList<Transaction> result = violation switch
        {
            "lost transaction" => [TestTransactions.Create("a")],
            "duplicate id" => [TestTransactions.Create("a"), TestTransactions.Create("a")],
            _ => [TestTransactions.Create("a"), null!]
        };
        _adapter.OnConvert = _ => result;

        var exception = await Assert.ThrowsAsync<TransactionJobException>(() => _job.RunAsync(CancellationToken.None));

        Assert.Equal(TransactionJobStage.Adapt, exception.Stage);
        Assert.DoesNotContain("save", _calls);
    }

    [Fact]
    public async Task RunAsync_FailureMessage_NamesProviderButNotInnerMessage()
    {
        _adapter.OnConvert = _ => throw new FormatException("שם מעביר: ישראל ישראלי 123456782");

        var exception = await Assert.ThrowsAsync<TransactionJobException>(() => _job.RunAsync(CancellationToken.None));

        Assert.Contains(ProviderName, exception.Message);
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
