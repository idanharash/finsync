using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FinSync.Abstractions;
using FinSync.Application;
using FinSync.Registration;

namespace FinSync.ConsoleHost;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var rawTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IProviderRegistration))
            .Select(RawTypeOf)
            .Distinct()
            .ToList();

        if (rawTypes.Count == 0)
            throw new InvalidOperationException(
                "No transaction provider is registered. Register providers before calling AddApplication().");

        foreach (var rawType in rawTypes)
        {
            EnsureSingleImplementation(services, typeof(ICollector<>).MakeGenericType(rawType), rawType);
            EnsureSingleImplementation(services, typeof(IAdapter<>).MakeGenericType(rawType), rawType);
            EnsureSingleImplementation(services, typeof(ITransactionMasker<>).MakeGenericType(rawType), rawType);
        }

        foreach (var rawType in rawTypes)
            services.TryAddEnumerable(ServiceDescriptor.Transient(
                typeof(ITransactionJob),
                typeof(TransactionJob<>).MakeGenericType(rawType)));

        services.TryAddSingleton<ProviderRegistrationsCompleted>();

        return services;
    }

    private static Type RawTypeOf(ServiceDescriptor descriptor)
    {
        if (!descriptor.IsKeyedService
            && descriptor.ImplementationInstance is IProviderRegistration registration
            && registration.GetType().IsGenericType
            && registration.GetType().GetGenericTypeDefinition() == typeof(ProviderRegistration<>))
            return registration.RawType;

        throw new InvalidOperationException("Provider registrations must be added with AddProvider().");
    }

    private static void EnsureSingleImplementation(IServiceCollection services, Type serviceType, Type rawType)
    {
        var registrations = services
            .Where(descriptor => descriptor.ServiceType == serviceType && !descriptor.IsKeyedService)
            .ToList();

        if (registrations.Count == 0)
            throw new InvalidOperationException(
                $"Provider {rawType.Name} is incomplete: {DisplayName(serviceType)} is not registered.");

        if (registrations.Count > 1)
            throw new InvalidOperationException(
                $"Provider {rawType.Name} has conflicting registrations for {DisplayName(serviceType)}.");

        var implementationType = registrations[0].ImplementationType
            ?? registrations[0].ImplementationInstance?.GetType();

        if (implementationType is not null && !serviceType.IsAssignableFrom(implementationType))
            throw new InvalidOperationException(
                $"Provider {rawType.Name} registers {implementationType.Name}, which does not implement {DisplayName(serviceType)}.");
    }

    private static string DisplayName(Type serviceType) =>
        $"{serviceType.Name[..serviceType.Name.IndexOf('`')]}<{serviceType.GetGenericArguments()[0].Name}>";
}
