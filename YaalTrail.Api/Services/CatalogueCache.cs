using Microsoft.Extensions.Caching.Memory;

namespace YaalTrail.Api.Services;

// NFR-01, NFR-02: the database is remote, so the published place list and the
// categories are kept in memory for a short time instead of being queried on every request.
public interface ICatalogueCache
{
    Task<T> GetOrLoadAsync<T>(string key, Func<Task<T>> load);

    // Admin services must call this after creating, updating, deleting or
    // (un)publishing a place or category, so visitors see the change at once.
    void Invalidate();
}

public class CatalogueCache : ICatalogueCache
{
    public const string PublishedPlacesKey = "catalogue:published-places";
    public const string CategoriesKey = "catalogue:categories";

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    public CatalogueCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrLoadAsync<T>(string key, Func<Task<T>> load)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached != null)
            return cached;

        T value = await load();
        _cache.Set(key, value, Lifetime);
        return value;
    }

    public void Invalidate()
    {
        _cache.Remove(PublishedPlacesKey);
        _cache.Remove(CategoriesKey);
    }
}
