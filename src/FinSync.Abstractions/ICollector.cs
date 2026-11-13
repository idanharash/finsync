namespace FinSync.Abstractions;

public interface ICollector<TRaw>
{
    Task<IReadOnlyList<TRaw>> CollectAsync(CancellationToken ct);
}
