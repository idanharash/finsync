namespace FinSync.Domain;

public sealed record MaskedDescription
{
    public string Value { get; }

    internal MaskedDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Masked description must not be empty.", nameof(value));

        Value = value;
    }

    public override string ToString() => Value;
}
