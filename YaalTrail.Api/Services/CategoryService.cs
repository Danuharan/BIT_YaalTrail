using Microsoft.EntityFrameworkCore;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;

namespace YaalTrail.Api.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ICatalogueCache _cache;

    public CategoryService(AppDbContext db, ICatalogueCache cache)
    {
        _db = db;
        _cache = cache;
    }

    // FR-02: categories used by the visitor filter.
    // NFR-02: served from the cache; one SQL query when the cache is empty.
    public async Task<List<CategoryDto>> GetAllAsync()
    {
        IReadOnlyList<CategoryDto> categories = await _cache.GetOrLoadAsync<IReadOnlyList<CategoryDto>>(
            CatalogueCache.CategoriesKey,
            async () => await _db.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.IconRef))
                .ToListAsync());

        // Return a copy so callers can never change the cached list.
        return categories.ToList();
    }
}
