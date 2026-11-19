using FinSync.Domain;

namespace FinSync.Abstractions;

public interface ISensitiveDataMasker
{
    IReadOnlyCollection<string> CommonSensitiveLabels { get; }

    MaskedDescription Mask(
        string description,
        IReadOnlyCollection<string> sensitiveValues,
        IReadOnlyCollection<string> trustedValues);
}
