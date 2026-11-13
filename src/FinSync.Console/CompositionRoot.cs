using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinSync.ConsoleHost;

public static class CompositionRoot
{
    public const string ApplicationSettingsFile = "appsettings.json";
    public const string ProviderSettingsPattern = "*settings.json";
    public const string LocalSettingsPattern = "*settings.local.json";

    public static IReadOnlyList<string> ProviderSettingsFiles(string basePath) =>
        SettingsFiles(basePath, ProviderSettingsPattern)
            .Where(file => !string.Equals(file, ApplicationSettingsFile, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public static IReadOnlyList<string> LocalSettingsFiles(string basePath) =>
        SettingsFiles(basePath, LocalSettingsPattern);

    public static IConfiguration LoadConfiguration(string basePath)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(ApplicationSettingsFile, optional: false);

        foreach (var file in ProviderSettingsFiles(basePath).Concat(LocalSettingsFiles(basePath)))
            builder.AddJsonFile(file, optional: false);

        return builder.Build();
    }

    private static IReadOnlyList<string> SettingsFiles(string basePath, string pattern) =>
        Directory.EnumerateFiles(basePath, pattern)
            .Select(file => Path.GetFileName(file))
            .Order(StringComparer.Ordinal)
            .ToList();

    public static ServiceProvider BuildValidatedProvider(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
