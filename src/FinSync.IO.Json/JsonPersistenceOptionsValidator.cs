using Microsoft.Extensions.Options;

namespace FinSync.IO.Json;

public sealed class JsonPersistenceOptionsValidator : IValidateOptions<JsonPersistenceOptions>
{
    private const string OutputDirectorySetting =
        $"{JsonPersistenceOptions.SectionName}:{nameof(JsonPersistenceOptions.OutputDirectory)}";

    public ValidateOptionsResult Validate(string? name, JsonPersistenceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            return ValidateOptionsResult.Fail($"{OutputDirectorySetting} is required.");

        if (options.OutputDirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return ValidateOptionsResult.Fail($"{OutputDirectorySetting} contains invalid path characters.");

        return ValidateOptionsResult.Success;
    }
}
