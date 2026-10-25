namespace YaalTrail.Api.Services;

// "Today" and "now" for visitors are in Sri Lanka time, not server time.
public static class SriLankaTime
{
    public static DateTime Now(TimeProvider clock)
    {
        return TimeZoneInfo.ConvertTime(clock.GetUtcNow(), GetTimeZone()).DateTime;
    }

    // "Asia/Colombo" is the IANA id; Windows without ICU may only know the Windows id.
    private static TimeZoneInfo GetTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
        }
    }
}
