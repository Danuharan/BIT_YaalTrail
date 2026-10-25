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

    public CategoryService(AppDbContext db)
    {
        _db = db;
    }

    // FR-02: categories used by the visitor filter.
    public async Task<List<CategoryDto>> GetAllAsync()
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.IconRef))
            .ToListAsync();
    }
}
