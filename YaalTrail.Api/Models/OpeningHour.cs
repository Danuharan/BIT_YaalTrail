namespace YaalTrail.Api.Models;

public class OpeningHour
{
    public int Id { get; set; }
    public int PlaceId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public bool IsClosedAllDay { get; set; }

    public Place? Place { get; set; }
}
