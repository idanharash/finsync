using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FinSync.ConsoleHost;
using FinSync.Domain;

namespace FinSync.Tests.ConsoleHost;

public class CollectionPeriodRegistrationTests
{
    private static readonly FixedTimeProvider Today = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));

    private static DateOnly Date(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static ServiceProvider Build(string? month)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CollectionPeriod:Month"] = month })
            .Build();

        return new ServiceCollection()
            .AddSingleton<TimeProvider>(Today)
            .AddCollectionPeriod(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Theory]
    [InlineData(null, "2026-10-01", "2026-10-31")]
    [InlineData("2026-09", "2026-09-01", "2026-09-30")]
    public void AddCollectionPeriod_ResolvesPreviousOrConfiguredMonth(string? month, string from, string to)
    {
        using var provider = Build(month);
        provider.GetRequiredService<IStartupValidator>().Validate();

        var period = provider.GetRequiredService<CollectionPeriod>();

        Assert.Equal((Date(from), Date(to)), (period.From, period.To));
    }

    [Theory]
    [InlineData("2026-13", "CollectionPeriod:Month must use the yyyy-MM format")]
    [InlineData("2026-11", "CollectionPeriod:Month must be a month that has already ended")]
    public void AddCollectionPeriod_InvalidMonth_FailsStartupValidation(string month, string expectedMessage)
    {
        using var provider = Build(month);

        var exception = Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(expectedMessage, exception.ToString());
    }
}
