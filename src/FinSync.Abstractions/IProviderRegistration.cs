namespace FinSync.Abstractions;

public interface IProviderRegistration
{
    Type RawType { get; }

    string ProviderName { get; }
}
