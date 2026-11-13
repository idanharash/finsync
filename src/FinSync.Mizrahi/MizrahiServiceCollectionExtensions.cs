using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using FinSync.Domain;
using FinSync.Registration;

namespace FinSync.Mizrahi;

public static class MizrahiServiceCollectionExtensions
{
    public static IServiceCollection AddMizrahiBank(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<MizrahiOptions>()
            .Bind(configuration.GetSection(MizrahiOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MizrahiOptions>, MizrahiOptionsValidator>());

        services
            .AddOptions<MizrahiMaskingOptions>()
            .Bind(configuration.GetSection(MizrahiMaskingOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<MizrahiMaskingOptions>, MizrahiMaskingOptionsValidator>());

        return services.AddProvider<MizrahiTransaction, MizrahiCollector, MizrahiAdapter, MizrahiTransactionMasker>(
            nameof(TransactionResource.MizrahiBank));
    }
}
