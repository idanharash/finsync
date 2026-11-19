using FinSync.Domain;

namespace FinSync.Masking;

public interface ISensitiveDataMasker
{
    IReadOnlyCollection<string> CommonSensitiveLabels { get; }

    MaskedDescription Mask(
        string description,
        IReadOnlyCollection<string> sensitiveValues,
        IReadOnlyCollection<string> trustedValues);
}
