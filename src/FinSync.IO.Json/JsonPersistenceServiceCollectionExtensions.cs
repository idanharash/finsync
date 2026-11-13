using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using FinSync.Abstractions;

namespace FinSync.IO.Json;

public static class JsonPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddJsonPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var otherWriterRegistered = services.Any(descriptor =>
            descriptor.ServiceType == typeof(ITransactionWriter)
            && (descriptor.IsKeyedService || descriptor.ImplementationType != typeof(JsonTransactionWriter)));

        if (otherWriterRegistered)
            throw new InvalidOperationException("Another transaction writer is already registered.");

        services
            .AddOptions<JsonPersistenceOptions>()
            .Bind(configuration.GetSection(JsonPersistenceOptions.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<JsonPersistenceOptions>, JsonPersistenceOptionsValidator>());
        services.TryAddSingleton<ITransactionWriter, JsonTransactionWriter>();

        return services;
    }
}
