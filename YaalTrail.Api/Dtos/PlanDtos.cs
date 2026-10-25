namespace YaalTrail.Api.Dtos;

// POST api/plan/calculate body. Stateless: the browser keeps the plan (FR-33, NFR-16).
// StartTime "HH:mm" (default 08:00, FR-23), PlanDate "yyyy-MM-dd" (default today).
// ManualOrder: place ids in the visitor's order, skips nearest-neighbour (FR-25).
// DurationOverrides: place id -> visit minutes (FR-27).
public record PlanRequestDto(
    string? StartTime,
    string? PlanDate,
    List<int>? PlaceIds,
    List<int>? ManualOrder,
    Dictionary<int, int>? DurationOverrides);

// FR-29, FR-30, NFR-10: Type is a fixed code for the UI icon, Text is the label (NFR-25).
public record PlanWarningDto(string Type, string Text);

// FR-28: one stop in visiting order. Arrival and Departure are "HH:mm",
// or null when the stop could not be scheduled before the latest arrival (NFR-10).
public record PlanStopDto(
    int Order,
    int PlaceId,
    string Name,
    string Category,
    decimal Latitude,
    decimal Longitude,
    double DistanceFromPreviousKm,
    int TravelMinutesFromPrevious,
    int VisitMinutes,
    string? Arrival,
    string? Departure,
    bool IsScheduled,
    List<PlanWarningDto> Warnings);

// FR-24: the plan ends back at the reference location.
public record PlanReturnLegDto(double DistanceKm, int TravelMinutes, string ArrivalBack);

// FR-32: totals cover the scheduled stops and the return leg.
public record PlanTotalsDto(
    double TotalTravelDistanceKm,
    int TotalTravelMinutes,
    int TotalVisitMinutes,
    int TotalElapsedMinutes,
    string EndTime,
    int ScheduledCount,
    int UnscheduledCount);

public record PlanResponseDto(
    string PlanDate,
    string Day,
    string StartTime,
    bool IsManualOrder,
    List<PlanStopDto> Stops,
    PlanReturnLegDto ReturnLeg,
    PlanTotalsDto Totals,
    string? Advisory);
