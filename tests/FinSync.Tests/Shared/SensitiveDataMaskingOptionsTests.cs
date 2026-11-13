using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FinSync.Abstractions;
using FinSync.Shared;

namespace FinSync.Tests.Shared;

public class SensitiveDataMaskingOptionsTests
{
    private const string Description = "כספונט 109(י) - 7044";

    private static ServiceProvider Build(IConfiguration configuration) =>
        new ServiceCollection()
            .AddTransactionMasking(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    private static string MaskWith(IConfiguration configuration)
    {
        using var provider = Build(configuration);
        provider.GetRequiredService<IStartupValidator>().Validate();
        return provider.GetRequiredService<ISensitiveDataMasker>().Mask(Description, [], []).Value;
    }

    [Fact]
    public void AddTransactionMasking_ShippedSettings_KeepLastTwoDigitsOfIdentifiers()
    {
        Assert.Equal("כספונט 109(י) - **44", MaskWith(ShippedSettings.Configuration()));
    }

    [Fact]
    public void AddTransactionMasking_BindsParametersFromConfiguration()
    {
        var masked = MaskWith(ShippedSettings.Configuration(new Dictionary<string, string?>
        {
            ["Masking:VisibleTrailingDigits"] = "0",
            ["Masking:MinimumIdentifierDigits"] = "3"
        }));

        Assert.Equal("כספונט ***(י) - ****", masked);
    }

    [Theory]
    [InlineData("Masking:VisibleTrailingDigits", "3")]
    [InlineData("Masking:MinimumIdentifierDigits", "7")]
    [InlineData("Masking:MinimumReliableSensitiveValueLength", "1")]
    public void AddTransactionMasking_WeakeningOrInvalidParameter_FailsStartupValidation(string setting, string value)
    {
        using var provider = Build(ShippedSettings.Configuration(new Dictionary<string, string?> { [setting] = value }));

        var exception = Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains($"{setting} must be between", exception.ToString());
    }

    [Fact]
    public void AddTransactionMasking_MissingSection_FailsStartupValidation()
    {
        using var provider = Build(new ConfigurationBuilder().Build());

        var exception = Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("Masking:MinimumIdentifierDigits must be between", exception.ToString());
        Assert.Contains("Masking:SensitiveLabels must contain at least one label", exception.ToString());
    }

    [Fact]
    public void Constructor_InvalidOptions_Throws()
    {
        var options = ShippedSettings.SharedMasking();
        options.VisibleTrailingDigits = 4;

        Assert.Throws<ArgumentException>(() => new SensitiveDataMasker(options));
    }
}
