namespace YaalTrail.Api.Configuration;

// SRS 6.4: reference location, permitted radius and average speed.
// The numbers live in the appsettings "Planner" section, not in code.
public class PlannerOptions
{
    public const string SectionName = "Planner";

    public decimal ReferenceLatitude { get; set; }
    public decimal ReferenceLongitude { get; set; }
    public decimal RadiusKm { get; set; }
    public decimal AverageSpeedKmh { get; set; }

    // TBD-02: road distance = straight-line distance * RoadFactor.
    public decimal RoadFactor { get; set; } = 1.3m;

    // FR-31, BR-06: advisory when a plan has more places than this.
    public int MaxRecommendedPlaces { get; set; } = 6;

    // NFR-10: no arrival is scheduled later than this time ("HH:mm").
    public string LatestArrival { get; set; } = "18:00";
}
