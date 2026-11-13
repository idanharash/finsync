using System.Globalization;

namespace FinSync.Domain;

public sealed record CollectionPeriod
{
    public const string MonthFormat = "yyyy-MM";

    public DateOnly From { get; }

    public DateOnly To { get; }

    private CollectionPeriod(DateOnly firstDayOfMonth)
    {
        From = firstDayOfMonth;
        To = firstDayOfMonth.AddMonths(1).AddDays(-1);
    }

    public static CollectionPeriod PreviousMonth(DateOnly today) => new(FirstDayOfMonth(today).AddMonths(-1));

    public static CollectionPeriod ForMonth(string month) =>
        TryParseMonth(month, out var firstDay)
            ? new CollectionPeriod(firstDay)
            : throw new FormatException($"Collection month must use the {MonthFormat} format.");

    public static bool TryParseMonth(string? value, out DateOnly firstDay) =>
        DateOnly.TryParseExact(value?.Trim(), MonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out firstDay);

    public static DateOnly FirstDayOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
