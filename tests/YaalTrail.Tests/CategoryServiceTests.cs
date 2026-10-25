using Microsoft.Extensions.Caching.Memory;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// FR-02 visitor categories, FR-46 admin category management, NFR-02 cache.
public class CategoryServiceTests
{
    private readonly AppDbContext _db = TestData.NewDatabase();

    private void AddCategories(params string[] names)
    {
        foreach (string name in names) _db.Categories.Add(new Category { Name = name });
        _db.SaveChanges();
    }

    // ---------- visitor list ----------

    [Fact]
    public async Task GetAllAsync_Categories_ReturnsThemSortedByName()
    {
        AddCategories("Religious", "Dining", "Heritage");
        var service = new CategoryService(_db, new CatalogueCache(new MemoryCache(new MemoryCacheOptions())));

        List<CategoryDto> categories = await service.GetAllAsync();

        Assert.Equal(new[] { "Dining", "Heritage", "Religious" }, categories.Select(c => c.Name));
    }

    // The caller gets a copy, so changing it cannot change the cached list.
    [Fact]
    public async Task GetAllAsync_CallerChangesList_CachedListIsUnchanged()
    {
        AddCategories("Religious");
        var service = new CategoryService(_db, new CatalogueCache(new MemoryCache(new MemoryCacheOptions())));

        (await service.GetAllAsync()).Clear();

        Assert.Single(await service.GetAllAsync());
    }

    // ---------- CatalogueCache ----------

    [Fact]
    public async Task GetOrLoadAsync_SecondCall_DoesNotLoadAgain()
    {
        var cache = new CatalogueCache(new MemoryCache(new MemoryCacheOptions()));
        int loads = 0;
        Task<string> Load() { loads++; return Task.FromResult("value"); }

        await cache.GetOrLoadAsync(CatalogueCache.CategoriesKey, Load);
        await cache.GetOrLoadAsync(CatalogueCache.CategoriesKey, Load);

        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Invalidate_AfterLoad_NextCallLoadsAgain()
    {
        var cache = new CatalogueCache(new MemoryCache(new MemoryCacheOptions()));
        int loads = 0;
        Task<string> Load() { loads++; return Task.FromResult("value"); }

        await cache.GetOrLoadAsync(CatalogueCache.PublishedPlacesKey, Load);
        cache.Invalidate();
        await cache.GetOrLoadAsync(CatalogueCache.PublishedPlacesKey, Load);

        Assert.Equal(2, loads);
    }

    // ---------- FR-46 admin ----------

    private (AdminCategoryService service, SpyCatalogueCache cache) Admin()
    {
        var cache = new SpyCatalogueCache();
        return (new AdminCategoryService(_db, cache), cache);
    }

    [Fact]
    public async Task CreateAsync_NewName_SavesTrimmedNameAndInvalidatesCache()
    {
        var (service, cache) = Admin();

        AdminCategoryDto created = await service.CreateAsync(new AdminCategoryRequestDto("  Beaches ", " Sandy places ", null));

        Assert.Equal("Beaches", created.Name);
        Assert.Equal("Sandy places", created.Description);
        Assert.Equal(0, created.PlaceCount);
        Assert.Equal(1, cache.InvalidateCount);
    }

    [Fact]
    public async Task CreateAsync_NameAlreadyUsedInOtherCase_Throws()
    {
        AddCategories("Heritage");
        var (service, cache) = Admin();

        await Assert.ThrowsAsync<AdminRequestException>(() =>
            service.CreateAsync(new AdminCategoryRequestDto("HERITAGE", null, null)));
        Assert.Equal(0, cache.InvalidateCount);
    }

    [Fact]
    public async Task CreateAsync_BlankName_Throws()
    {
        var (service, _) = Admin();

        await Assert.ThrowsAsync<AdminRequestException>(() =>
            service.CreateAsync(new AdminCategoryRequestDto("   ", null, null)));
    }

    [Fact]
    public async Task UpdateAsync_Rename_ChangesNameAndInvalidatesCache()
    {
        AddCategories("Heritge");
        var (service, cache) = Admin();
        int id = _db.Categories.Single().Id;

        AdminCategoryDto? renamed = await service.UpdateAsync(id, new AdminCategoryRequestDto("Heritage", null, "heritage"));

        Assert.Equal("Heritage", renamed!.Name);
        Assert.Equal("heritage", renamed.IconRef);
        Assert.Equal(1, cache.InvalidateCount);
    }

    // Renaming to its own name (e.g. only changing the description) is allowed.
    [Fact]
    public async Task UpdateAsync_KeepSameName_IsAllowed()
    {
        AddCategories("Heritage");
        var (service, _) = Admin();
        int id = _db.Categories.Single().Id;

        AdminCategoryDto? updated = await service.UpdateAsync(id, new AdminCategoryRequestDto("Heritage", "New text", null));

        Assert.Equal("New text", updated!.Description);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        var (service, _) = Admin();

        Assert.Null(await service.UpdateAsync(999, new AdminCategoryRequestDto("X", null, null)));
    }

    [Fact]
    public async Task DeleteAsync_CategoryWithPlaces_ThrowsConflictAndKeepsIt()
    {
        var category = new Category { Name = "Heritage" };
        _db.Places.Add(new Place { Name = "Jaffna Fort", Category = category });
        _db.SaveChanges();
        var (service, cache) = Admin();

        var error = await Assert.ThrowsAsync<AdminConflictException>(() => service.DeleteAsync(category.Id));

        Assert.Contains("1 place(s)", error.Message);
        Assert.Single(_db.Categories);
        Assert.Equal(0, cache.InvalidateCount);
    }

    [Fact]
    public async Task DeleteAsync_EmptyCategory_DeletesIt()
    {
        AddCategories("Unused");
        var (service, cache) = Admin();

        bool deleted = await service.DeleteAsync(_db.Categories.Single().Id);

        Assert.True(deleted);
        Assert.Empty(_db.Categories);
        Assert.Equal(1, cache.InvalidateCount);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        var (service, _) = Admin();

        Assert.False(await service.DeleteAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_Admin_IncludesPlaceCounts()
    {
        var category = new Category { Name = "Heritage" };
        _db.Places.AddRange(new Place { Name = "A", Category = category }, new Place { Name = "B", Category = category });
        _db.SaveChanges();
        var (service, _) = Admin();

        AdminCategoryDto row = Assert.Single(await service.GetAllAsync());

        Assert.Equal(2, row.PlaceCount);
    }
}
