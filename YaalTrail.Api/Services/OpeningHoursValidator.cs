using YaalTrail.Api.Models;

namespace YaalTrail.Api.Services;

public enum OpeningHoursCheck
{
    Ok,
    ArrivesBeforeOpening, // FR-30
    ArrivesAfterClosing,  // FR-29
    ClosedAllDay,
    HoursNotRecorded      // FR-12: no row or no times for that day
}

public interface IOpeningHoursValidator
{
    OpeningHoursCheck Check(IEnumerable<OpeningHour> hours, DayOfWeek day, TimeOnly arrival);
}

// Pure logic, no database access, so it can be unit tested (NFR-23).
public class OpeningHoursValidator : IOpeningHoursValidator
{
    public OpeningHoursCheck Check(IEnumerable<OpeningHour> hours, DayOfWeek day, TimeOnly arrival)
    {
        // BR-07: hours may differ by day of week, so use the row for the plan's day.
        OpeningHour? row = hours.FirstOrDefault(h => h.DayOfWeek == day);

        if (row == null)
            return OpeningHoursCheck.HoursNotRecorded;

        if (row.IsClosedAllDay)
            return OpeningHoursCheck.ClosedAllDay;

        if (row.OpenTime == null || row.CloseTime == null)
            return OpeningHoursCheck.HoursNotRecorded;

        // FR-30
        if (arrival < row.OpenTime.Value)
            return OpeningHoursCheck.ArrivesBeforeOpening;

        // FR-29: arriving exactly at closing time is too late (same rule as FR-09 "open now").
        if (arrival >= row.CloseTime.Value)
            return OpeningHoursCheck.ArrivesAfterClosing;

        return OpeningHoursCheck.Ok;
    }
}
