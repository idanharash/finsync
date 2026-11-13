using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FinSync.Application;
using FinSync.ConsoleHost;
using FinSync.IO.Json;
using FinSync.Mizrahi;
using FinSync.Shared;
using FinSync.Tests.IO;

namespace FinSync.Tests.ConsoleHost;

public sealed class CompositionRootTests : IDisposable
{
    private const string SecretUsername = "user-4351";

    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private static ServiceProvider BuildProgramServices(string? password)
    {
        var configuration = ShippedSettings.Configuration(new Dictionary<string, string?>
        {
            ["MizrahiBank:Username"] = SecretUsername,
            ["MizrahiBank:Password"] = password
        });

        var services = new ServiceCollection();
        services.AddCollectionPeriod(configuration);
        services.AddMizrahiBank(configuration);
        services.AddJsonPersistence(configuration);
        services.AddTransactionMasking(configuration);
        services.AddApplication();

        return CompositionRoot.BuildValidatedProvider(services);
    }

    [Fact]
    public void ProgramRegistrations_ValidConfiguration_ResolveNamedMizrahiJob()
    {
        using var provider = BuildProgramServices(password: "pa55-w0rd!");
        provider.GetRequiredService<IStartupValidator>().Validate();

        var job = Assert.Single(provider.GetServices<ITransactionJob>());
        Assert.IsType<TransactionJob<MizrahiTransaction>>(job);
        Assert.Equal("MizrahiBank", job.ProviderName);
    }

    [Fact]
    public void ProgramRegistrations_MissingPassword_FailStartupValidationWithoutSecrets()
    {
        using var provider = BuildProgramServices(password: null);

        var exception = Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("MizrahiBank:Password is required", exception.ToString());
        Assert.DoesNotContain(SecretUsername, exception.ToString());
    }

    [Fact]
    public async Task LoadConfiguration_LoadsApplicationThenProviderThenLocalFiles()
    {
        await File.WriteAllTextAsync(
            _directory.File(CompositionRoot.ApplicationSettingsFile),
            """{ "JsonPersistence": { "OutputDirectory": "output" } }""");
        await File.WriteAllTextAsync(
            _directory.File("bankMizrahisettings.json"),
            """{ "MizrahiBank": { "LoginTimeoutSeconds": 180, "PageTimeoutSeconds": 60 } }""");
        await File.WriteAllTextAsync(
            _directory.File("bankMizrahisettings.local.json"),
            """{ "MizrahiBank": { "Username": "user-4351", "LoginTimeoutSeconds": 90 } }""");
        await File.WriteAllTextAsync(
            _directory.File("bankDiscountsettings.local.json"),
            """{ "DiscountBank": { "Username": "user-7788" } }""");

        var configuration = CompositionRoot.LoadConfiguration(_directory.Path);

        Assert.Equal(new[] { "bankMizrahisettings.json" }, CompositionRoot.ProviderSettingsFiles(_directory.Path));
        Assert.Equal(new[] { "bankDiscountsettings.local.json", "bankMizrahisettings.local.json" }, CompositionRoot.LocalSettingsFiles(_directory.Path));
        Assert.Equal("output", configuration["JsonPersistence:OutputDirectory"]);
        Assert.Equal("60", configuration["MizrahiBank:PageTimeoutSeconds"]);
        Assert.Equal("90", configuration["MizrahiBank:LoginTimeoutSeconds"]);
        Assert.Equal("user-4351", configuration["MizrahiBank:Username"]);
        Assert.Equal("user-7788", configuration["DiscountBank:Username"]);
    }

    [Fact]
    public void ShippedSettingsFiles_ContainNoCredentials()
    {
        var consoleDirectory = ShippedSettings.ConsoleDirectory;
        var builder = new ConfigurationBuilder()
            .SetBasePath(consoleDirectory)
            .AddJsonFile(CompositionRoot.ApplicationSettingsFile, optional: false);
        foreach (var file in CompositionRoot.ProviderSettingsFiles(consoleDirectory))
            builder.AddJsonFile(file, optional: false);

        var credentials = builder.Build().AsEnumerable()
            .Where(setting => setting.Key.EndsWith(":Username", StringComparison.Ordinal) || setting.Key.EndsWith(":Password", StringComparison.Ordinal))
            .ToList();
        var filledCredentialKeys = credentials
            .Where(setting => !string.IsNullOrEmpty(setting.Value))
            .Select(setting => setting.Key)
            .ToList();

        Assert.NotEmpty(CompositionRoot.ProviderSettingsFiles(consoleDirectory));
        Assert.NotEmpty(credentials);
        Assert.True(
            filledCredentialKeys.Count == 0,
            $"Committed settings contain credentials in {string.Join(", ", filledCredentialKeys)}. Move them to a git-ignored *settings.local.json file.");
    }
}
