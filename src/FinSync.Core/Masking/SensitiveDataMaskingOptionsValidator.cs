using Microsoft.Extensions.Options;

namespace FinSync.Shared;

public sealed class SensitiveDataMaskingOptionsValidator : IValidateOptions<SensitiveDataMaskingOptions>
{
    public const int MaximumVisibleTrailingDigits = 2;
    public const int MinimumIdentifierDigitsLowerBound = 3;
    public const int MinimumIdentifierDigitsUpperBound = 6;
    public const int MinimumReliableSensitiveValueLengthLowerBound = 2;
    public const int MinimumReliableSensitiveValueLengthUpperBound = 10;

    public ValidateOptionsResult Validate(string? name, SensitiveDataMaskingOptions options)
    {
        var failures = new List<string>();

        if (options.VisibleTrailingDigits is < 0 or > MaximumVisibleTrailingDigits)
            failures.Add($"{Setting(nameof(options.VisibleTrailingDigits))} must be between 0 and {MaximumVisibleTrailingDigits}.");

        if (options.MinimumIdentifierDigits is < MinimumIdentifierDigitsLowerBound or > MinimumIdentifierDigitsUpperBound)
            failures.Add(
                $"{Setting(nameof(options.MinimumIdentifierDigits))} must be between {MinimumIdentifierDigitsLowerBound} and {MinimumIdentifierDigitsUpperBound}.");

        if (options.MinimumReliableSensitiveValueLength is < MinimumReliableSensitiveValueLengthLowerBound
            or > MinimumReliableSensitiveValueLengthUpperBound)
            failures.Add(
                $"{Setting(nameof(options.MinimumReliableSensitiveValueLength))} must be between {MinimumReliableSensitiveValueLengthLowerBound} and {MinimumReliableSensitiveValueLengthUpperBound}.");

        var labels = options.SensitiveLabels ?? [];
        if (labels.Count == 0)
            failures.Add($"{Setting(nameof(options.SensitiveLabels))} must contain at least one label.");
        else if (labels.Any(string.IsNullOrWhiteSpace))
            failures.Add($"{Setting(nameof(options.SensitiveLabels))} must not contain empty labels.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string Setting(string property) => $"{SensitiveDataMaskingOptions.SectionName}:{property}";
}
