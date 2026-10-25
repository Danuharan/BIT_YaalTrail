namespace YaalTrail.Api.Dtos;

// SRS 6.4: reference location, radius and average speed come from configuration.
public record PlannerSettingsDto(
    decimal ReferenceLatitude,
    decimal ReferenceLongitude,
    decimal RadiusKm,
    decimal AverageSpeedKmh);
