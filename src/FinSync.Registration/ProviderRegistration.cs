using FinSync.Abstractions;

namespace FinSync.Registration;

public sealed class ProviderRegistration<TRaw> : IProviderRegistration
{
    public ProviderRegistration(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));

        ProviderName = providerName;
    }

    public Type RawType => typeof(TRaw);

    public string ProviderName { get; }
}
