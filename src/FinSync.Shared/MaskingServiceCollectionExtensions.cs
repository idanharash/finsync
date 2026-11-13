using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using FinSync.Abstractions;

namespace FinSync.Shared;

public static class MaskingServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionMasking(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SensitiveDataMaskingOptions>()
            .Bind(configuration.GetSection(SensitiveDataMaskingOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SensitiveDataMaskingOptions>, SensitiveDataMaskingOptionsValidator>());
        services.TryAddSingleton<ISensitiveDataMasker>(provider =>
            new SensitiveDataMasker(provider.GetRequiredService<IOptions<SensitiveDataMaskingOptions>>().Value));

        return services;
    }
}
