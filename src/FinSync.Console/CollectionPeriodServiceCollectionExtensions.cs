using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using FinSync.Domain;

namespace FinSync.ConsoleHost;

public static class CollectionPeriodServiceCollectionExtensions
{
    public static IServiceCollection AddCollectionPeriod(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<CollectionPeriodOptions>()
            .Bind(configuration.GetSection(CollectionPeriodOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<CollectionPeriodOptions>, CollectionPeriodOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => Resolve(
            provider.GetRequiredService<IOptions<CollectionPeriodOptions>>().Value,
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }

    internal static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static CollectionPeriod Resolve(CollectionPeriodOptions options, TimeProvider timeProvider) =>
        string.IsNullOrWhiteSpace(options.Month)
            ? CollectionPeriod.PreviousMonth(Today(timeProvider))
            : CollectionPeriod.ForMonth(options.Month);
}
