namespace YaalTrail.Api.Models;

public class PlaceImage
{
    public int Id { get; set; }
    public int PlaceId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }

    public Place? Place { get; set; }
}
