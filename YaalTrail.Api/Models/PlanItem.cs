namespace YaalTrail.Api.Models;

public class PlanItem
{
    public int Id { get; set; }
    public int VisitPlanId { get; set; }
    public int? PlaceId { get; set; }
    public string PlaceNameSnapshot { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
    public TimeOnly ArrivalTime { get; set; }
    public TimeOnly DepartureTime { get; set; }
    public bool HasTimingWarning { get; set; }

    public VisitPlan? VisitPlan { get; set; }
    public Place? Place { get; set; }
}
