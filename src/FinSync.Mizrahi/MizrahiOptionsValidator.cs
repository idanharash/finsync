using Microsoft.Extensions.Options;

namespace FinSync.Mizrahi;

public sealed class MizrahiOptionsValidator : IValidateOptions<MizrahiOptions>
{
    public const int MinimumLoginTimeoutSeconds = 30;
    public const int MaximumLoginTimeoutSeconds = 900;
    public const int MinimumPageTimeoutSeconds = 10;
    public const int MaximumPageTimeoutSeconds = 300;

    public ValidateOptionsResult Validate(string? name, MizrahiOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.LoginUrl, UriKind.Absolute, out var loginUrl) || loginUrl.Scheme != Uri.UriSchemeHttps)
            failures.Add($"{Setting(nameof(MizrahiOptions.LoginUrl))} must be an absolute https URL.");

        if (string.IsNullOrWhiteSpace(options.Username))
            failures.Add($"{Setting(nameof(MizrahiOptions.Username))} is required.");

        if (string.IsNullOrWhiteSpace(options.Password))
            failures.Add($"{Setting(nameof(MizrahiOptions.Password))} is required.");

        if (options.LoginTimeoutSeconds is < MinimumLoginTimeoutSeconds or > MaximumLoginTimeoutSeconds)
            failures.Add(
                $"{Setting(nameof(MizrahiOptions.LoginTimeoutSeconds))} must be between {MinimumLoginTimeoutSeconds} and {MaximumLoginTimeoutSeconds}.");

        if (options.PageTimeoutSeconds is < MinimumPageTimeoutSeconds or > MaximumPageTimeoutSeconds)
            failures.Add(
                $"{Setting(nameof(MizrahiOptions.PageTimeoutSeconds))} must be between {MinimumPageTimeoutSeconds} and {MaximumPageTimeoutSeconds}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string Setting(string property) => $"{MizrahiOptions.SectionName}:{property}";
}
