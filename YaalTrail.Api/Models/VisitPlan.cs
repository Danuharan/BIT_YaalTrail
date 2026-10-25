namespace YaalTrail.Api.Models;

public class VisitPlan
{
    public int Id { get; set; }
    public string ShareToken { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public DateOnly PlanDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<PlanItem> Items { get; set; } = new();
}
