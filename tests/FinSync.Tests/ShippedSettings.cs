using Microsoft.Extensions.Configuration;
using FinSync.ConsoleHost;
using FinSync.Mizrahi;
using FinSync.Shared;

namespace FinSync.Tests;

public static class ShippedSettings
{
    public const string MizrahiSettingsFile = "bankMizrahisettings.json";

    public static string ConsoleDirectory { get; } = FindConsoleDirectory();

    public static IConfiguration Configuration(IDictionary<string, string?>? overrides = null)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(ConsoleDirectory)
            .AddJsonFile(CompositionRoot.ApplicationSettingsFile, optional: false)
            .AddJsonFile(MizrahiSettingsFile, optional: false);

        if (overrides is not null)
            builder.AddInMemoryCollection(overrides);

        return builder.Build();
    }

    public static SensitiveDataMaskingOptions SharedMasking() =>
        Configuration().GetSection(SensitiveDataMaskingOptions.SectionName).Get<SensitiveDataMaskingOptions>()!;

    public static MizrahiMaskingOptions MizrahiMasking() =>
        Configuration().GetSection(MizrahiMaskingOptions.SectionName).Get<MizrahiMaskingOptions>()!;

    public static SensitiveDataMasker CreateSensitiveDataMasker() => new(SharedMasking());

    private static string FindConsoleDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FinSync.sln")))
            directory = directory.Parent;

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("FinSync.sln was not found above the test output directory."),
            "src",
            "FinSync.Console");
    }
}
