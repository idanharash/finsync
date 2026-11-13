using System.Text.Json;
using Microsoft.Extensions.Options;
using FinSync.Domain;
using FinSync.IO.Json;
using FinSync.Shared;

namespace FinSync.Tests.IO;

public sealed class JsonTransactionWriterTests : IDisposable
{
    private const string MizrahiFile = "MizrahiBank.json";

    private static readonly SensitiveDataMasker Masker = ShippedSettings.CreateSensitiveDataMasker();
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly TemporaryDirectory _directory = new();
    private readonly JsonTransactionWriter _writer;

    public JsonTransactionWriterTests()
    {
        _writer = CreateWriter(_directory.Path);
    }

    public void Dispose() => _directory.Dispose();

    public sealed record StoredRecord(string Id, string Resource, string Date, decimal Amount, string Description);

    public static TheoryData<string> InvalidExistingFiles()
    {
        var mizrahiId = Create("a").Id.Value;
        var calId = Create("cal", resource: TransactionResource.Cal).Id.Value;
        var record = $$"""{"id":"{{mizrahiId}}","resource":"MizrahiBank","date":"2026-10-09","amount":1,"description":"משכורת"}""";

        return new TheoryData<string>
        {
            "not json",
            $$"""[{"id":"{{calId}}","resource":"Cal","date":"2026-10-09","amount":1,"description":"משכורת"}]""",
            $"[{record},{record}]"
        };
    }

    private static JsonTransactionWriter CreateWriter(string outputDirectory) =>
        new(Options.Create(new JsonPersistenceOptions { OutputDirectory = outputDirectory }));

    private static Transaction Create(
        string identity,
        decimal amount = -607.90m,
        string description = "כספונט 109(י)",
        TransactionResource resource = TransactionResource.MizrahiBank,
        int day = 9) =>
        new(
            TransactionId.FromIdentity(resource, identity),
            resource,
            new DateOnly(2026, 10, day),
            amount,
            Masker.Mask(description, [], []));

    private static List<StoredRecord> ReadStored(string path) =>
        JsonSerializer.Deserialize<List<StoredRecord>>(File.ReadAllText(path), ReadOptions)!;

    [Fact]
    public async Task SaveAsync_FirstRun_CreatesDirectoryAndWritesAllFieldsAsReadableText()
    {
        var outputDirectory = Path.Combine(_directory.Path, "output");
        var transaction = Create("a", description: "משכורת (י)");

        await CreateWriter(outputDirectory).SaveAsync([transaction], CancellationToken.None);

        var path = Path.Combine(outputDirectory, MizrahiFile);
        var stored = Assert.Single(ReadStored(path));
        Assert.Equal(new StoredRecord(transaction.Id.Value, "MizrahiBank", "2026-10-09", -607.90m, "משכורת (י)"), stored);
        Assert.Contains("משכורת (י)", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task SaveAsync_DifferentResources_WritesOneFilePerResource()
    {
        await _writer.SaveAsync([Create("a"), Create("b", resource: TransactionResource.Cal)], CancellationToken.None);

        Assert.Equal("MizrahiBank", Assert.Single(ReadStored(_directory.File(MizrahiFile))).Resource);
        Assert.Equal("Cal", Assert.Single(ReadStored(_directory.File("Cal.json"))).Resource);
    }

    [Fact]
    public async Task SaveAsync_RepeatedRuns_UpdateByIdInsertNewKeepExistingAndNeverDuplicate()
    {
        await _writer.SaveAsync([Create("a", amount: -100m), Create("b")], CancellationToken.None);
        IReadOnlyList<Transaction> secondRun = [Create("a", amount: -200m), Create("c")];

        await _writer.SaveAsync(secondRun, CancellationToken.None);
        await _writer.SaveAsync(secondRun, CancellationToken.None);

        var stored = ReadStored(_directory.File(MizrahiFile)).ToDictionary(record => record.Id);
        Assert.Equal(3, stored.Count);
        Assert.Equal(-200m, stored[Create("a").Id.Value].Amount);
        Assert.Contains(Create("b").Id.Value, stored.Keys);
        Assert.Contains(Create("c").Id.Value, stored.Keys);
        Assert.Equal(new[] { MizrahiFile }, Directory.EnumerateFiles(_directory.Path).Select(Path.GetFileName));
    }

    [Fact]
    public async Task SaveAsync_EmptyBatch_LeavesExistingFileUnchanged()
    {
        await _writer.SaveAsync([Create("a")], CancellationToken.None);
        var before = await File.ReadAllTextAsync(_directory.File(MizrahiFile));

        await _writer.SaveAsync([], CancellationToken.None);

        Assert.Equal(before, await File.ReadAllTextAsync(_directory.File(MizrahiFile)));
    }

    [Fact]
    public async Task SaveAsync_DuplicateIdsInBatch_ThrowsAndWritesNothing()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _writer.SaveAsync([Create("a"), Create("a")], CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(_directory.Path));
    }

    [Theory]
    [MemberData(nameof(InvalidExistingFiles))]
    public async Task SaveAsync_InvalidExistingFile_ThrowsAndLeavesFileUnchanged(string existingContent)
    {
        var path = _directory.File(MizrahiFile);
        await File.WriteAllTextAsync(path, existingContent);

        await Assert.ThrowsAsync<InvalidDataException>(() => _writer.SaveAsync([Create("b")], CancellationToken.None));

        Assert.Equal(existingContent, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task SaveAsync_ReplaceFailsOnWindows_KeepsOriginalAndLeavesNoTemporaryFile()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await _writer.SaveAsync([Create("a")], CancellationToken.None);
        var path = _directory.File(MizrahiFile);
        var before = await File.ReadAllTextAsync(path);
        File.SetAttributes(path, FileAttributes.ReadOnly);

        await Assert.ThrowsAnyAsync<Exception>(() => _writer.SaveAsync([Create("b")], CancellationToken.None));

        Assert.Equal(before, await File.ReadAllTextAsync(path));
        Assert.Single(Directory.EnumerateFiles(_directory.Path));
    }

    [Fact]
    public async Task SaveAsync_PersistsOnlyMaskedDescription()
    {
        var transaction = new Transaction(
            TransactionId.FromIdentity(TransactionResource.MizrahiBank, "a"),
            TransactionResource.MizrahiBank,
            new DateOnly(2026, 10, 13),
            55m,
            Masker.Mask("שם מעביר: ישראל ישראלי | חשבון: 803480", ["ישראל ישראלי", "803480"], []));

        await _writer.SaveAsync([transaction], CancellationToken.None);

        var content = await File.ReadAllTextAsync(_directory.File(MizrahiFile));
        Assert.DoesNotContain("ישראל ישראלי", content);
        Assert.DoesNotContain("803480", content);
        Assert.Contains("שם מעביר: ***", content);
    }
}
