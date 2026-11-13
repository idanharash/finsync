using Microsoft.Extensions.Options;
using FinSync.Abstractions;

namespace FinSync.Mizrahi;

public sealed class MizrahiMaskingOptionsValidator(ISensitiveDataMasker sensitiveDataMasker) : IValidateOptions<MizrahiMaskingOptions>
{
    public ValidateOptionsResult Validate(string? name, MizrahiMaskingOptions options)
    {
        var failures = new List<string>();
        var sensitive = Labels(options.SensitiveLabels, nameof(options.SensitiveLabels), failures);
        sensitive.UnionWith(sensitiveDataMasker.CommonSensitiveLabels.Select(MizrahiMaskingOptions.NormalizeLabel));
        var nonSensitive = Labels(options.NonSensitiveLabels, nameof(options.NonSensitiveLabels), failures);
        var trusted = Labels(options.TrustedNumericLabels, nameof(options.TrustedNumericLabels), failures);

        if (nonSensitive.Count == 0)
            failures.Add($"{Setting(nameof(options.NonSensitiveLabels))} must contain at least one label.");

        foreach (var label in sensitive.Where(label => nonSensitive.Contains(label) || trusted.Contains(label)))
            failures.Add($"{MizrahiMaskingOptions.SectionName}: '{label}' cannot be both sensitive and non-sensitive or trusted.");

        foreach (var label in trusted.Where(label => !nonSensitive.Contains(label)))
            failures.Add(
                $"{Setting(nameof(options.TrustedNumericLabels))}: '{label}' must also be listed in {nameof(options.NonSensitiveLabels)}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static HashSet<string> Labels(IReadOnlyList<string>? configured, string property, List<string> failures)
    {
        var labels = (configured ?? []).Select(MizrahiMaskingOptions.NormalizeLabel).ToList();
        if (labels.Any(label => label.Length == 0))
            failures.Add($"{Setting(property)} must not contain empty labels.");

        return labels.Where(label => label.Length > 0).ToHashSet(StringComparer.Ordinal);
    }

    private static string Setting(string property) => $"{MizrahiMaskingOptions.SectionName}:{property}";
}
