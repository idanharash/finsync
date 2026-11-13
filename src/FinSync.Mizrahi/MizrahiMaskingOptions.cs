namespace FinSync.Mizrahi;

public sealed class MizrahiMaskingOptions
{
    public const string SectionName = "MizrahiBank:Masking";

    public List<string> SensitiveLabels { get; set; } = [];

    public List<string> NonSensitiveLabels { get; set; } = [];

    public List<string> TrustedNumericLabels { get; set; } = [];

    public static string NormalizeLabel(string? label) => (label ?? string.Empty).Trim().TrimEnd(':').Trim();
}
