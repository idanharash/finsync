using Microsoft.Extensions.Options;
using FinSync.Domain;

namespace FinSync.ConsoleHost;

public sealed class CollectionPeriodOptionsValidator(TimeProvider timeProvider) : IValidateOptions<CollectionPeriodOptions>
{
    private const string MonthSetting = $"{CollectionPeriodOptions.SectionName}:{nameof(CollectionPeriodOptions.Month)}";

    public ValidateOptionsResult Validate(string? name, CollectionPeriodOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Month))
            return ValidateOptionsResult.Success;

        if (!CollectionPeriod.TryParseMonth(options.Month, out var firstDay))
            return ValidateOptionsResult.Fail($"{MonthSetting} must use the {CollectionPeriod.MonthFormat} format.");

        return firstDay < CollectionPeriod.FirstDayOfMonth(CollectionPeriodServiceCollectionExtensions.Today(timeProvider))
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{MonthSetting} must be a month that has already ended.");
    }
}
