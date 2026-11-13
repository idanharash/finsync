using FinSync.Domain;

namespace FinSync.Tests.Domain;

public class CollectionPeriodTests
{
    [Fact]
    public void PreviousMonthAndForMonth_CoverWholeCalendarMonths()
    {
        var previous = CollectionPeriod.PreviousMonth(new DateOnly(2027, 1, 1));
        var configured = CollectionPeriod.ForMonth("2026-09");

        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), (previous.From, previous.To));
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), (configured.From, configured.To));
    }

    [Fact]
    public void ForMonth_InvalidFormat_Throws()
    {
        Assert.Throws<FormatException>(() => CollectionPeriod.ForMonth("09/2026"));
    }
}
