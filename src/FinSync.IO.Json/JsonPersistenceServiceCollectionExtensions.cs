using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FinSync.IO.Json;

public static class JsonPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddJsonPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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
