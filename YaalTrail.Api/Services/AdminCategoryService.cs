using Microsoft.EntityFrameworkCore;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;

namespace YaalTrail.Api.Services;

// FR-46: create, rename and delete categories (BR-02: administrators only).
public interface IAdminCategoryService
{
    Task<List<AdminCategoryDto>> GetAllAsync();
    Task<AdminCategoryDto?> GetAsync(int id);
    Task<AdminCategoryDto> CreateAsync(AdminCategoryRequestDto request);
    Task<AdminCategoryDto?> UpdateAsync(int id, AdminCategoryRequestDto request);
    Task<bool> DeleteAsync(int id);
}

public class AdminCategoryService : IAdminCategoryService
{
    private readonly AppDbContext _db;
    private readonly ICatalogueCache _cache;

    public AdminCategoryService(AppDbContext db, ICatalogueCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<AdminCategoryDto>> GetAllAsync()
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new AdminCategoryDto(c.Id, c.Name, c.Description, c.IconRef, c.Places.Count))
            .ToListAsync();
    }

    public async Task<AdminCategoryDto?> GetAsync(int id)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new AdminCategoryDto(c.Id, c.Name, c.Description, c.IconRef, c.Places.Count))
            .FirstOrDefaultAsync();
    }

    public async Task<AdminCategoryDto> CreateAsync(AdminCategoryRequestDto request)
    {
        string name = await ValidateNameAsync(request.Name, excludeId: null);

        var category = new Category
        {
            Name = name,
            Description = EmptyToNull(request.Description),
            IconRef = EmptyToNull(request.IconRef),
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        _cache.Invalidate();

        return new AdminCategoryDto(category.Id, category.Name, category.Description, category.IconRef, 0);
    }

    // FR-46: rename (and edit description / icon).
    public async Task<AdminCategoryDto?> UpdateAsync(int id, AdminCategoryRequestDto request)
    {
        Category? category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
            return null;

        category.Name = await ValidateNameAsync(request.Name, excludeId: id);
        category.Description = EmptyToNull(request.Description);
        category.IconRef = EmptyToNull(request.IconRef);

        await _db.SaveChangesAsync();
        _cache.Invalidate();

        return await GetAsync(id);
    }

    // FR-46: a category that still has places cannot be deleted
    // (the database also refuses it: Place.CategoryId is OnDelete Restrict).
    public async Task<bool> DeleteAsync(int id)
    {
        Category? category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
            return false;

        int placeCount = await _db.Places.CountAsync(p => p.CategoryId == id);
        if (placeCount > 0)
            throw new AdminConflictException(
                $"Category '{category.Name}' has {placeCount} place(s). Move or delete them before deleting the category.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        _cache.Invalidate();
        return true;
    }

    // Names must be unique (ignoring case) so the visitor filter is not confusing.
    private async Task<string> ValidateNameAsync(string name, int? excludeId)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0)
            throw new AdminRequestException("Category name is required.");

        string lower = trimmed.ToLower();
        bool exists = await _db.Categories.AnyAsync(c => c.Name.ToLower() == lower && c.Id != excludeId);
        if (exists)
            throw new AdminRequestException($"A category named '{trimmed}' already exists.");

        return trimmed;
    }

    private static string? EmptyToNull(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
