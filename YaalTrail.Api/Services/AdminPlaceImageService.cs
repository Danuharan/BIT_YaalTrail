using Microsoft.EntityFrameworkCore;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;

namespace YaalTrail.Api.Services;

// FR-11, FR-45: add, reorder and remove a place's photos (BR-02: administrators only).
// The file goes to photo storage; the database keeps only its public URL.
public interface IAdminPlaceImageService
{
    // Returns null if the place does not exist.
    Task<AdminPlaceImageDto?> AddAsync(int placeId, Stream content, long length, string? caption, int adminId);

    // imageIds: every photo of the place, in the new order. Returns null if the place does not exist.
    Task<List<AdminPlaceImageDto>?> ReorderAsync(int placeId, List<int> imageIds, int adminId);

    // Returns false if the place or the photo does not exist.
    Task<bool> DeleteAsync(int placeId, int imageId, int adminId);
}

public class AdminPlaceImageService : IAdminPlaceImageService
{
    public const int MaxPhotosPerPlace = 5;          // FR-11
    public const long MaxFileBytes = 3 * 1024 * 1024; // 3 MB
    public const int MaxCaptionLength = 200;

    private readonly AppDbContext _db;
    private readonly IPhotoStorage _storage;
    private readonly ICatalogueCache _cache;
    private readonly TimeProvider _clock;
    private readonly ILogger<AdminPlaceImageService> _logger;

    public AdminPlaceImageService(AppDbContext db, IPhotoStorage storage, ICatalogueCache cache,
        TimeProvider clock, ILogger<AdminPlaceImageService> logger)
    {
        _db = db;
        _storage = storage;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AdminPlaceImageDto?> AddAsync(int placeId, Stream content, long length, string? caption, int adminId)
    {
        Place? place = await LoadPlaceAsync(placeId);
        if (place == null)
            return null;

        if (place.Images.Count >= MaxPhotosPerPlace)
            throw new AdminRequestException($"A place can have at most {MaxPhotosPerPlace} photos. Delete one before adding another.");

        caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (caption?.Length > MaxCaptionLength)
            throw new AdminRequestException($"The caption can be at most {MaxCaptionLength} characters.");

        byte[] bytes = await ReadLimitedAsync(content, length);

        // Check the file's first bytes, not its name or the Content-Type the browser sent.
        ImageType type = ImageSignature.Detect(bytes)
            ?? throw new AdminRequestException("Only JPEG, PNG or WebP images can be uploaded. This file is not one of them.");

        string url = await _storage.UploadAsync(bytes, type);

        var image = new PlaceImage
        {
            ImageUrl = url,
            Caption = caption,
            SortOrder = place.Images.Count == 0 ? 0 : place.Images.Max(i => i.SortOrder) + 1, // added at the end
        };
        place.Images.Add(image);
        Touch(place, adminId);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // The file is already in storage; do not leave it there without a database row.
            await DeleteFileQuietlyAsync(url);
            throw;
        }

        _cache.Invalidate();
        return ToDto(image);
    }

    public async Task<List<AdminPlaceImageDto>?> ReorderAsync(int placeId, List<int> imageIds, int adminId)
    {
        Place? place = await LoadPlaceAsync(placeId);
        if (place == null)
            return null;

        // The new order must list every photo of this place exactly once.
        var current = place.Images.Select(i => i.Id).ToHashSet();
        bool sameIds = imageIds.Count == current.Count && imageIds.Distinct().Count() == imageIds.Count && imageIds.All(current.Contains);
        if (!sameIds)
            throw new AdminRequestException("The new order must list every photo of this place exactly once.");

        for (int i = 0; i < imageIds.Count; i++)
            place.Images.First(image => image.Id == imageIds[i]).SortOrder = i;

        Touch(place, adminId);
        await _db.SaveChangesAsync();
        _cache.Invalidate();

        return Ordered(place.Images).Select(ToDto).ToList();
    }

    public async Task<bool> DeleteAsync(int placeId, int imageId, int adminId)
    {
        Place? place = await LoadPlaceAsync(placeId);
        PlaceImage? image = place?.Images.FirstOrDefault(i => i.Id == imageId);
        if (place == null || image == null)
            return false;

        _db.PlaceImages.Remove(image);
        place.Images.Remove(image);

        // Close the gap so the remaining photos stay numbered 0, 1, 2, ...
        int order = 0;
        foreach (PlaceImage remaining in Ordered(place.Images))
            remaining.SortOrder = order++;

        Touch(place, adminId);
        await _db.SaveChangesAsync();
        _cache.Invalidate();

        // Best effort: the photo is already gone from the place even if the file stays behind.
        await DeleteFileQuietlyAsync(image.ImageUrl);
        return true;
    }

    private Task<Place?> LoadPlaceAsync(int placeId)
    {
        return _db.Places.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == placeId);
    }

    // Reads the upload into memory, refusing empty files and anything over 3 MB.
    // Reading stops one byte past the limit, so a wrong length cannot get a large file in.
    private static async Task<byte[]> ReadLimitedAsync(Stream content, long length)
    {
        if (length <= 0)
            throw new AdminRequestException("The file is empty.");
        if (length > MaxFileBytes)
            throw new AdminRequestException(TooLargeMessage());

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxFileBytes)
                throw new AdminRequestException(TooLargeMessage());
        }

        if (buffer.Length == 0)
            throw new AdminRequestException("The file is empty.");

        return buffer.ToArray();
    }

    private static string TooLargeMessage() => $"The photo is larger than {MaxFileBytes / (1024 * 1024)} MB. Please choose a smaller file.";

    private async Task DeleteFileQuietlyAsync(string url)
    {
        try
        {
            await _storage.DeleteAsync(url);
        }
        catch (PhotoStorageException)
        {
            _logger.LogWarning("A photo file could not be removed from storage and was left behind.");
        }
    }

    // FR-48: changing the photos is a change to the place.
    private void Touch(Place place, int adminId)
    {
        place.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        place.MaintainedByAdminId = adminId;
    }

    private static IEnumerable<PlaceImage> Ordered(IEnumerable<PlaceImage> images) =>
        images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id);

    private static AdminPlaceImageDto ToDto(PlaceImage image) =>
        new(image.Id, image.ImageUrl, image.Caption, image.SortOrder);
}
