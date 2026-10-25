namespace YaalTrail.Api.Models;

public class Place
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public string? TravelTips { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // FR-48: the administrator who last created or modified this place.
    // Nullable because seeded places have no admin, and deleting an admin keeps the place.
    public int? MaintainedByAdminId { get; set; }

    public Category? Category { get; set; }
    public AdminUser? MaintainedByAdmin { get; set; }
    public List<PlaceImage> Images { get; set; } = new();
    public List<OpeningHour> OpeningHours { get; set; } = new();
}
