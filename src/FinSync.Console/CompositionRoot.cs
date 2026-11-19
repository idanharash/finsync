using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinSync.ConsoleHost;

public static class CompositionRoot
{
    public const string ApplicationSettingsFile = "appsettings.json";
    public const string MizrahiSettingsFile = "bankMizrahisettings.json";

    /// <summary>Optional, git-ignored overrides: the place for credentials.</summary>
    public const string MizrahiLocalSettingsFile = "bankMizrahisettings.local.json";

    // Loaded in this order, later files override earlier ones. The bank files are optional so that a missing
    // file is reported by startup validation (exit code 2), naming the settings, rather than as a crash.
    public static IConfiguration LoadConfiguration(string basePath) =>
        new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(ApplicationSettingsFile, optional: false)
            .AddJsonFile(MizrahiSettingsFile, optional: true)
            .AddJsonFile(MizrahiLocalSettingsFile, optional: true)
            .Build();

    public static ServiceProvider BuildValidatedProvider(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
