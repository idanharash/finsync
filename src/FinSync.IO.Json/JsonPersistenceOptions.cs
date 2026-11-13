namespace FinSync.IO.Json;

public sealed class JsonPersistenceOptions
{
    public const string SectionName = "JsonPersistence";

    public string OutputDirectory { get; set; } = string.Empty;
}
