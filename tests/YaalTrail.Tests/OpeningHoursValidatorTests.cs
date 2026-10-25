using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// FR-29, FR-30, FR-12, BR-07
public class OpeningHoursValidatorTests
{
    private readonly OpeningHoursValidator _validator = new();

    // Open 09:00 to 17:00 on Monday only.
    private static List<OpeningHour> MondayNineToFive() => new()
    {
        new OpeningHour { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeOnly(9, 0), CloseTime = new TimeOnly(17, 0) },
    };

    [Fact]
    public void Check_ArrivalWithinOpeningHours_ReturnsOk()
    {
        var result = _validator.Check(MondayNineToFive(), DayOfWeek.Monday, new TimeOnly(10, 30));

        Assert.Equal(OpeningHoursCheck.Ok, result);
    }

    [Fact]
    public void Check_ArrivalExactlyAtOpeningTime_ReturnsOk()
    {
        var result = _validator.Check(MondayNineToFive(), DayOfWeek.Monday, new TimeOnly(9, 0));

        Assert.Equal(OpeningHoursCheck.Ok, result);
    }

    [Fact]
    public void Check_ArrivalBeforeOpening_ReturnsArrivesBeforeOpening()
    {
        var result = _validator.Check(MondayNineToFive(), DayOfWeek.Monday, new TimeOnly(8, 59));

        Assert.Equal(OpeningHoursCheck.ArrivesBeforeOpening, result);
    }

    [Fact]
    public void Check_ArrivalAfterClosing_ReturnsArrivesAfterClosing()
    {
        var result = _validator.Check(MondayNineToFive(), DayOfWeek.Monday, new TimeOnly(17, 30));

        Assert.Equal(OpeningHoursCheck.ArrivesAfterClosing, result);
    }

    // Arriving exactly at closing time is too late (same rule as "open now", FR-09).
    [Fact]
    public void Check_ArrivalExactlyAtClosingTime_ReturnsArrivesAfterClosing()
    {
        var result = _validator.Check(MondayNineToFive(), DayOfWeek.Monday, new TimeOnly(17, 0));

        Assert.Equal(OpeningHoursCheck.ArrivesAfterClosing, result);
    }

    [Fact]
    public void Check_OneMinuteBeforeClosing_ReturnsOk()
    {
        var result = _validator.Check(MondayNineToFive(), DayOfWeek.Monday, new TimeOnly(16, 59));

        Assert.Equal(OpeningHoursCheck.Ok, result);
    }

    [Fact]
    public void Check_DayMarkedClosedAllDay_ReturnsClosedAllDay()
    {
        var hours = new List<OpeningHour> { new() { DayOfWeek = DayOfWeek.Sunday, IsClosedAllDay = true } };

        var result = _validator.Check(hours, DayOfWeek.Sunday, new TimeOnly(10, 0));

        Assert.Equal(OpeningHoursCheck.ClosedAllDay, result);
    }

    [Fact]
    public void Check_NoRowForThatDay_ReturnsHoursNotRecorded()
    {
        var result = _validator.Check(MondayNineToFive(), DayOfWeek.Tuesday, new TimeOnly(10, 0));

        Assert.Equal(OpeningHoursCheck.HoursNotRecorded, result);
    }

    [Fact]
    public void Check_NoOpeningHoursAtAll_ReturnsHoursNotRecorded()
    {
        var result = _validator.Check(new List<OpeningHour>(), DayOfWeek.Monday, new TimeOnly(10, 0));

        Assert.Equal(OpeningHoursCheck.HoursNotRecorded, result);
    }

    [Fact]
    public void Check_RowWithoutTimes_ReturnsHoursNotRecorded()
    {
        var hours = new List<OpeningHour> { new() { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeOnly(9, 0) } };

        var result = _validator.Check(hours, DayOfWeek.Monday, new TimeOnly(10, 0));

        Assert.Equal(OpeningHoursCheck.HoursNotRecorded, result);
    }

    // BR-07: hours can differ by day, so the row for the plan's day is the one used.
    [Fact]
    public void Check_DifferentHoursPerDay_UsesRowForRequestedDay()
    {
        var hours = new List<OpeningHour>
        {
            new() { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeOnly(9, 0), CloseTime = new TimeOnly(12, 0) },
            new() { DayOfWeek = DayOfWeek.Tuesday, OpenTime = new TimeOnly(14, 0), CloseTime = new TimeOnly(18, 0) },
        };

        Assert.Equal(OpeningHoursCheck.ArrivesAfterClosing, _validator.Check(hours, DayOfWeek.Monday, new TimeOnly(15, 0)));
        Assert.Equal(OpeningHoursCheck.Ok, _validator.Check(hours, DayOfWeek.Tuesday, new TimeOnly(15, 0)));
    }
}
