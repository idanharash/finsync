using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FinSync.Abstractions;

namespace FinSync.Registration;

public static class ProviderServiceCollectionExtensions
{
    public static IServiceCollection AddProvider<TRaw, TCollector, TAdapter, TMasker>(
        this IServiceCollection services,
        string providerName)
        where TCollector : class, ICollector<TRaw>
        where TAdapter : class, IAdapter<TRaw>
        where TMasker : class, ITransactionMasker<TRaw>
    {
        ArgumentNullException.ThrowIfNull(services);
        var registration = new ProviderRegistration<TRaw>(providerName);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ProviderRegistrationsCompleted)))
            throw new InvalidOperationException(
                $"Provider {typeof(TRaw).Name} must be registered before AddApplication() is called.");

        AddTransientOnce<ICollector<TRaw>, TCollector>(services);
        AddTransientOnce<IAdapter<TRaw>, TAdapter>(services);
        AddTransientOnce<ITransactionMasker<TRaw>, TMasker>(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProviderRegistration>(registration));

        return services;
    }

    private static void AddTransientOnce<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        var alreadyRegistered = services.Any(descriptor =>
            descriptor.ServiceType == typeof(TService)
            && !descriptor.IsKeyedService
            && descriptor.ImplementationType == typeof(TImplementation));

        if (!alreadyRegistered)
            services.AddTransient<TService, TImplementation>();
    }
}
