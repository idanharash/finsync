namespace FinSync.Shared;

public sealed class SensitiveDataMaskingOptions
{
    public const string SectionName = "Masking";

    public int VisibleTrailingDigits { get; set; }

    public int MinimumIdentifierDigits { get; set; }

    public int MinimumReliableSensitiveValueLength { get; set; }

    public List<string> SensitiveLabels { get; set; } = [];
}
