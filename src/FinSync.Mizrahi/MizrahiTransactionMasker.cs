using Microsoft.Extensions.Options;
using FinSync.Abstractions;
using FinSync.Domain;

namespace FinSync.Mizrahi;

public sealed class MizrahiTransactionMasker : ITransactionMasker<MizrahiTransaction>
{
    private const string DetailSeparator = " | ";

    private readonly ISensitiveDataMasker _sensitiveDataMasker;
    private readonly HashSet<string> _sensitiveLabels;
    private readonly HashSet<string> _nonSensitiveLabels;
    private readonly HashSet<string> _trustedNumericLabels;

    public MizrahiTransactionMasker(ISensitiveDataMasker sensitiveDataMasker, IOptions<MizrahiMaskingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(sensitiveDataMasker);
        ArgumentNullException.ThrowIfNull(options);

        _sensitiveDataMasker = sensitiveDataMasker;
        _sensitiveLabels = Normalized([.. options.Value.SensitiveLabels, .. sensitiveDataMasker.CommonSensitiveLabels]);
        _nonSensitiveLabels = Normalized(options.Value.NonSensitiveLabels);
        _trustedNumericLabels = Normalized(options.Value.TrustedNumericLabels);
    }

    public MaskedDescription Mask(MizrahiTransaction rawTransaction)
    {
        ArgumentNullException.ThrowIfNull(rawTransaction);
        ArgumentNullException.ThrowIfNull(rawTransaction.Details);

        var details = rawTransaction.Details
            .Where(detail => detail is not null && !string.IsNullOrWhiteSpace(detail.Value))
            .ToList();

        return _sensitiveDataMasker.Mask(
            ComposeDescription(rawTransaction.Description, details),
            details.Where(IsSensitive).Select(detail => detail.Value.Trim()).ToList(),
            details.Where(IsTrusted).Select(detail => detail.Value.Trim()).ToList());
    }

    private bool IsSensitive(MizrahiTransactionDetail detail)
    {
        var label = MizrahiMaskingOptions.NormalizeLabel(detail.Label);
        return _sensitiveLabels.Contains(label) || !_nonSensitiveLabels.Contains(label);
    }

    private bool IsTrusted(MizrahiTransactionDetail detail) =>
        !IsSensitive(detail) && _trustedNumericLabels.Contains(MizrahiMaskingOptions.NormalizeLabel(detail.Label));

    private static string ComposeDescription(string description, IReadOnlyList<MizrahiTransactionDetail> details) =>
        string.Join(DetailSeparator, details.Select(DisplayDetail).Prepend(description));

    private static string DisplayDetail(MizrahiTransactionDetail detail)
    {
        var label = MizrahiMaskingOptions.NormalizeLabel(detail.Label);
        var value = detail.Value.Trim();

        return label.Length == 0 ? value : $"{label}: {value}";
    }

    private static HashSet<string> Normalized(IReadOnlyList<string>? labels) =>
        (labels ?? []).Select(MizrahiMaskingOptions.NormalizeLabel).Where(label => label.Length > 0).ToHashSet(StringComparer.Ordinal);
}
