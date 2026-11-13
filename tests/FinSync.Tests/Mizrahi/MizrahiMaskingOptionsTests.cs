using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FinSync.Abstractions;
using FinSync.Mizrahi;
using FinSync.Shared;

namespace FinSync.Tests.Mizrahi;

public class MizrahiMaskingOptionsTests
{
    private static readonly MizrahiTransaction Transfer = new(
        "2026-10-13T00:00:00", "55", "העברה", "100200300", "1",
        [new("בנק:", "12-בנק הפועלים"), new("ערך נקוב:", "76780")]);

    private static ServiceProvider Build(IConfiguration configuration) =>
        new ServiceCollection()
            .AddTransactionMasking(configuration)
            .AddMizrahiBank(configuration)
            .BuildServiceProvider();

    private static ServiceProvider BuildShipped(Dictionary<string, string?> maskingOverrides)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["MizrahiBank:Username"] = "user",
            ["MizrahiBank:Password"] = "password"
        };
        foreach (var (key, value) in maskingOverrides)
            overrides[$"{MizrahiMaskingOptions.SectionName}:{key}"] = value;

        return Build(ShippedSettings.Configuration(overrides));
    }

    [Fact]
    public void ShippedRules_AreValid()
    {
        Assert.True(new MizrahiMaskingOptionsValidator(ShippedSettings.CreateSensitiveDataMasker()).Validate(null, ShippedSettings.MizrahiMasking()).Succeeded);
    }

    [Fact]
    public void AddMizrahiBank_BindsLabelsFromConfiguration()
    {
        using var provider = BuildShipped(new() { ["NonSensitiveLabels:4"] = "מטבע" });
        provider.GetRequiredService<IStartupValidator>().Validate();

        var masked = provider.GetRequiredService<ITransactionMasker<MizrahiTransaction>>().Mask(Transfer).Value;

        Assert.Equal("העברה | בנק: *** | ערך נקוב: 76780", masked);
    }

    [Theory]
    [InlineData("SensitiveLabels:0", "שער", "cannot be both sensitive and non-sensitive or trusted")]
    [InlineData("TrustedNumericLabels:0", "מספר כרטיס", "must also be listed in NonSensitiveLabels")]
    [InlineData("NonSensitiveLabels:0", " : ", "must not contain empty labels")]
    public void AddMizrahiBank_InvalidOrConflictingLabels_FailStartupValidation(string key, string label, string expectedMessage)
    {
        using var provider = BuildShipped(new() { [key] = label });

        var exception = Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(expectedMessage, exception.ToString());
    }

    [Fact]
    public void Mask_CommonLabelsFromGeneralSettings_AreMasked()
    {
        var raw = Transfer with { Details = [new("חשבון:", "803480"), new("שם מוטב:", "דנה כהן")] };
        var masker = new MizrahiTransactionMasker(
            ShippedSettings.CreateSensitiveDataMasker(),
            Options.Create(ShippedSettings.MizrahiMasking()));

        Assert.Empty(ShippedSettings.MizrahiMasking().SensitiveLabels);
        Assert.Equal("העברה | חשבון: *** | שם מוטב: ***", masker.Mask(raw).Value);
    }

    [Fact]
    public void AddMizrahiBank_MissingMaskingRules_FailStartupValidation()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ShippedSettings.ConsoleDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MizrahiBank:LoginUrl"] = "https://www.mizrahi-tefahot.co.il/",
                ["MizrahiBank:Username"] = "user",
                ["MizrahiBank:Password"] = "password",
                ["MizrahiBank:LoginTimeoutSeconds"] = "180",
                ["MizrahiBank:PageTimeoutSeconds"] = "60"
            })
            .Build();
        using var provider = Build(configuration);

        var exception = Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("MizrahiBank:Masking:NonSensitiveLabels must contain at least one label", exception.ToString());
    }

    [Fact]
    public void Mask_SensitiveLabelTakesPrecedenceOverNonSensitiveAndTrusted()
    {
        var options = ShippedSettings.MizrahiMasking();
        options.SensitiveLabels.Add("ערך נקוב");
        var masker = new MizrahiTransactionMasker(ShippedSettings.CreateSensitiveDataMasker(), Options.Create(options));

        Assert.Equal("העברה | בנק: 12-בנק הפועלים | ערך נקוב: ***", masker.Mask(Transfer).Value);
    }
}
