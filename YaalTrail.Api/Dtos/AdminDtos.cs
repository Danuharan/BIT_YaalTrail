using System.ComponentModel.DataAnnotations;

namespace YaalTrail.Api.Dtos;

// ---------- SRS 4.7 authentication ----------

// FR-49
public record AdminLoginRequestDto(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(200)] string Password);

// FR-51: the signed access token and when it stops working.
public record AdminLoginResponseDto(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt,
    string DisplayName);

// ---------- SRS 4.6 place management ----------

// FR-44: one entry per day of the week. Day is "Monday" ... "Sunday",
// times are 24-hour "HH:mm". When IsClosed is true the times are ignored.
public record AdminOpeningHourDto(
    [Required] string Day,
    string? OpenTime,
    string? CloseTime,
    bool IsClosed);

// FR-39, FR-40: everything an administrator can set on a place.
public record AdminPlaceRequestDto(
    [Required, MaxLength(200)] string Name,
    [Range(1, int.MaxValue, ErrorMessage = "A category is required (BR-05).")] int CategoryId,
    [MaxLength(4000)] string? Description,
    [Range(-90, 90)] decimal Latitude,
    [Range(-180, 180)] decimal Longitude,
    // Road distance from the reference location. If left empty it is estimated (FR-26).
    [Range(0, 9999)] decimal? DistanceKm,
    [Range(0, 99_999_999)] decimal EntryFee,
    [MaxLength(4000)] string? TravelTips,
    bool IsPublished,
    List<AdminOpeningHourDto>? OpeningHours);

// GET api/admin/places: includes unpublished places.
public record AdminPlaceListItemDto(
    int Id,
    string Name,
    int CategoryId,
    string Category,
    decimal DistanceKm,
    bool IsPublished,
    DateTime UpdatedAt,
    string? MaintainedBy);

// GET api/admin/places/{id}
public record AdminPlaceDto(
    int Id,
    string Name,
    int CategoryId,
    string Category,
    string? Description,
    decimal Latitude,
    decimal Longitude,
    decimal DistanceKm,
    decimal EntryFee,
    string? TravelTips,
    bool IsPublished,
    List<AdminOpeningHourDto> OpeningHours,
    List<PlaceImageDto> Images,
    DateTime CreatedAt,      // FR-48
    DateTime UpdatedAt,      // FR-48
    int? MaintainedByAdminId,
    string? MaintainedBy,
    int SavedPlanCount);     // FR-41: lets the UI warn that saved plans will be affected

// ---------- FR-46 categories ----------

public record AdminCategoryRequestDto(
    [Required, MaxLength(100)] string Name,
    [MaxLength(500)] string? Description,
    [MaxLength(200)] string? IconRef);

public record AdminCategoryDto(int Id, string Name, string? Description, string? IconRef, int PlaceCount);
