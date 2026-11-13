using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using FinSync.Abstractions;
using FinSync.Domain;

namespace FinSync.IO.Json;

public sealed class JsonTransactionWriter(IOptions<JsonPersistenceOptions> options) : ITransactionWriter
{
    public const string FileExtension = ".json";
    private const string TemporaryFileExtension = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task SaveAsync(IReadOnlyList<Transaction> transactions, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ct.ThrowIfCancellationRequested();

        if (transactions.Count == 0)
            return;

        if (transactions.Select(transaction => transaction.Id).Distinct().Count() != transactions.Count)
            throw new ArgumentException("Transactions to save must have unique ids.", nameof(transactions));

        var directory = Path.GetFullPath(options.Value.OutputDirectory);
        Directory.CreateDirectory(directory);

        var files = new List<(string Path, IReadOnlyList<TransactionRecord> Records)>();
        foreach (var resourceGroup in transactions.GroupBy(transaction => transaction.Resource))
        {
            var path = Path.Combine(directory, $"{resourceGroup.Key}{FileExtension}");
            var existing = await ReadExistingAsync(path, resourceGroup.Key, ct);
            files.Add((path, Upsert(existing, resourceGroup.Select(TransactionRecord.From))));
        }

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            await WriteAtomicallyAsync(file.Path, file.Records, ct);
        }
    }

    private static async Task<IReadOnlyList<TransactionRecord>> ReadExistingAsync(
        string path,
        TransactionResource resource,
        CancellationToken ct)
    {
        if (!File.Exists(path))
            return [];

        var fileName = Path.GetFileName(path);
        List<TransactionRecord>? records;

        try
        {
            await using var stream = File.OpenRead(path);
            records = await JsonSerializer.DeserializeAsync<List<TransactionRecord>>(stream, SerializerOptions, ct);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{fileName} does not contain valid transaction JSON.", exception);
        }

        if (records is null || records.Any(record => record is null || !record.IsValidFor(resource)))
            throw new InvalidDataException($"{fileName} contains invalid transaction records.");

        if (records.Select(record => record.Id).Distinct().Count() != records.Count)
            throw new InvalidDataException($"{fileName} contains duplicate transaction ids.");

        return records;
    }

    private static IReadOnlyList<TransactionRecord> Upsert(
        IReadOnlyList<TransactionRecord> existing,
        IEnumerable<TransactionRecord> incoming)
    {
        var recordsById = existing.ToDictionary(record => record.Id);

        foreach (var record in incoming)
            recordsById[record.Id] = record;

        return recordsById.Values
            .OrderBy(record => record.Date)
            .ThenBy(record => record.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static async Task WriteAtomicallyAsync(string path, IReadOnlyList<TransactionRecord> records, CancellationToken ct)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}{TemporaryFileExtension}";

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, records, SerializerOptions, ct);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            else
                File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
