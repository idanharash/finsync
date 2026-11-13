using FinSync.Mizrahi;

namespace FinSync.Tests.Mizrahi;

public class MizrahiOptionsValidatorTests
{
    private const string SecretUsername = "user-4351";
    private const string SecretPassword = "pa55-w0rd!";

    private static readonly MizrahiOptionsValidator Validator = new();

    private static MizrahiOptions ValidOptions() => new()
    {
        LoginUrl = "https://www.mizrahi-tefahot.co.il/",
        Username = SecretUsername,
        Password = SecretPassword,
        LoginTimeoutSeconds = 180,
        PageTimeoutSeconds = 60
    };

    [Fact]
    public void Validate_ValidOptions_Succeeds()
    {
        Assert.True(Validator.Validate(null, ValidOptions()).Succeeded);
    }

    [Theory]
    [InlineData(nameof(MizrahiOptions.LoginUrl), "http://www.mizrahi-tefahot.co.il/", "MizrahiBank:LoginUrl must be an absolute https URL")]
    [InlineData(nameof(MizrahiOptions.Password), "", "MizrahiBank:Password is required")]
    public void Validate_InvalidSetting_FailsWithSettingNameAndNoCredentials(string setting, string value, string expectedMessage)
    {
        var options = ValidOptions();
        typeof(MizrahiOptions).GetProperty(setting)!.SetValue(options, value);

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(expectedMessage, result.FailureMessage);
        Assert.DoesNotContain(SecretUsername, result.FailureMessage);
        Assert.DoesNotContain(SecretPassword, result.FailureMessage);
    }

    [Fact]
    public void Validate_TimeoutsOutOfRange_Fail()
    {
        var options = ValidOptions();
        options.LoginTimeoutSeconds = 29;
        options.PageTimeoutSeconds = 301;

        var result = Validator.Validate(null, options);

        Assert.Contains("MizrahiBank:LoginTimeoutSeconds must be between 30 and 900", result.FailureMessage);
        Assert.Contains("MizrahiBank:PageTimeoutSeconds must be between 10 and 300", result.FailureMessage);
    }

    [Fact]
    public void Options_ToString_DoesNotExposeCredentials()
    {
        var text = ValidOptions().ToString();

        Assert.DoesNotContain(SecretUsername, text);
        Assert.DoesNotContain(SecretPassword, text);
    }
}
