namespace YaalTrail.Api.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconRef { get; set; }

    public List<Place> Places { get; set; } = new();
}
