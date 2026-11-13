using System.Text;
using System.Text.RegularExpressions;
using FinSync.Abstractions;
using FinSync.Domain;

namespace FinSync.Shared;

public sealed partial class SensitiveDataMasker : ISensitiveDataMasker
{
    public const string Redacted = "***";

    private readonly int _visibleTrailingDigits;
    private readonly int _minimumReliableSensitiveValueLength;
    private readonly Regex _identifierNumberPattern;

    public IReadOnlyCollection<string> CommonSensitiveLabels { get; }

    public SensitiveDataMasker(SensitiveDataMaskingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validation = new SensitiveDataMaskingOptionsValidator().Validate(null, options);
        if (validation.Failed)
            throw new ArgumentException(validation.FailureMessage, nameof(options));

        _visibleTrailingDigits = options.VisibleTrailingDigits;
        _minimumReliableSensitiveValueLength = options.MinimumReliableSensitiveValueLength;
        CommonSensitiveLabels = options.SensitiveLabels.ToArray();
        _identifierNumberPattern = new Regex(
            $@"(?<!\d)(?<!\d[.,/:])\d(?:[ -]?\d){{{options.MinimumIdentifierDigits - 1},}}(?!\d)(?![.,/:]\d)",
            RegexOptions.CultureInvariant);
    }

    public MaskedDescription Mask(
        string description,
        IReadOnlyCollection<string> sensitiveValues,
        IReadOnlyCollection<string> trustedValues)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(sensitiveValues);
        ArgumentNullException.ThrowIfNull(trustedValues);

        var text = Normalize(description);
        if (text.Length == 0)
            throw new ArgumentException("Description must not be empty.", nameof(description));

        var secrets = sensitiveValues
            .Select(Normalize)
            .Where(secret => secret.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(secret => secret.Length)
            .ToList();

        if (secrets.Any(secret => secret.Length < _minimumReliableSensitiveValueLength))
            return new MaskedDescription(Redacted);

        foreach (var secret in secrets)
            text = text.Replace(secret, Redacted, StringComparison.OrdinalIgnoreCase);

        text = EmailPattern().Replace(text, Redacted);
        var trusted = trustedValues.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        text = _identifierNumberPattern.Replace(
            text,
            match => trusted.Contains(match.Value) ? match.Value : MaskAllButTrailingDigits(match.Value));

        return new MaskedDescription(text);
    }

    private static string Normalize(string value)
    {
        var withoutFormatCharacters = FormatCharacterPattern().Replace(value.Normalize(NormalizationForm.FormC), string.Empty);
        return WhitespacePattern().Replace(withoutFormatCharacters, " ").Trim();
    }

    private string MaskAllButTrailingDigits(string value)
    {
        var digitsLeft = value.Count(char.IsAsciiDigit);
        var masked = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                masked.Append(character);
                continue;
            }

            masked.Append(digitsLeft > _visibleTrailingDigits ? '*' : character);
            digitsLeft--;
        }

        return masked.ToString();
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\p{Cf}")]
    private static partial Regex FormatCharacterPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
