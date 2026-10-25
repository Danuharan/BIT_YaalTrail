namespace YaalTrail.Api.Dtos;

// FR-01, FR-14: one catalogue row. Latitude and longitude let the map
// place a marker without loading every place's full detail.
public record PlaceListItemDto(
    int Id,
    string Name,
    string Category,
    decimal DistanceKm,
    string? PhotoUrl,
    string EntryFeeText,
    decimal Latitude,
    decimal Longitude);

// FR-05: list plus the number of matching places.
public record PlaceListResponseDto(int Count, List<PlaceListItemDto> Items);

public record OpeningHourDto(
    string Day,
    string OpenTime,
    string CloseTime,
    bool IsClosedAllDay);

public record PlaceImageDto(string ImageUrl, string? Caption);

// FR-08: full place details.
public record PlaceDetailDto(
    int Id,
    string Name,
    string Category,
    string Description,
    decimal DistanceKm,
    decimal Latitude,
    decimal Longitude,
    decimal EntryFee,
    string EntryFeeText,
    string TravelTips,
    string TodayOpenTime,
    string TodayCloseTime,
    bool IsOpenNow,
    List<OpeningHourDto> OpeningHours,
    List<PlaceImageDto> Images,
    DateTime UpdatedAt);
