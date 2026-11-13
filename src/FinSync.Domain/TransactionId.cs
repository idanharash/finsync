using System.Security.Cryptography;
using System.Text;

namespace FinSync.Domain;

public sealed record TransactionId
{
    private const int HexLength = 64;

    public string Value { get; }

    private TransactionId(string value) => Value = value;

    public static TransactionId FromIdentity(TransactionResource resource, params string[] identityParts)
    {
        if (!Enum.IsDefined(resource))
            throw new ArgumentOutOfRangeException(nameof(resource), "Unsupported transaction resource.");
        ArgumentNullException.ThrowIfNull(identityParts);
        if (identityParts.Length == 0)
            throw new ArgumentException("At least one identity part is required.", nameof(identityParts));
        if (identityParts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Identity parts must not be empty.", nameof(identityParts));

        var canonicalIdentity = string.Join('\n', identityParts
            .Prepend(resource.ToString())
            .Select(part => $"{part.Length}:{part}"));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIdentity));
        return new TransactionId(Convert.ToHexStringLower(hash));
    }

    public static TransactionId Parse(string value)
    {
        if (value is null || value.Length != HexLength || !value.All(char.IsAsciiHexDigitLower))
            throw new FormatException("Transaction id must be a 64 character lowercase hexadecimal string.");

        return new TransactionId(value);
    }

    public override string ToString() => Value;
}
