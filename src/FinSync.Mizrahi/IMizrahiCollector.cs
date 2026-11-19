namespace FinSync.Mizrahi;

/// <summary>Reads the raw transactions from the bank's website: the boundary to the browser and the bank.</summary>
public interface IMizrahiCollector
{
    Task<IReadOnlyList<MizrahiTransaction>> CollectAsync(CancellationToken ct);
}
