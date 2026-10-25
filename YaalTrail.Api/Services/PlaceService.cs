using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;

namespace YaalTrail.Api.Services;

public interface IPlaceService
{
    Task<PlaceListResponseDto> GetPlacesAsync(string? categories, string? search, string? sort);
    Task<PlaceDetailDto?> GetPlaceAsync(int id);
}

public class PlaceService : IPlaceService
{
    // FR-12: shown for any attribute that is not recorded.
    public const string NotAvailable = "Information not available";
    public const string FreeEntry = "Free entry";
    public const string Closed = "Closed";

    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public PlaceService(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PlaceListResponseDto> GetPlacesAsync(string? categories, string? search, string? sort)
    {
        // FR-07: visitors only ever see published places.
        IQueryable<Place> query = _db.Places.AsNoTracking().Where(p => p.IsPublished);

        // FR-02: filter by one or more category ids, e.g. "1,2".
        List<int> categoryIds = ParseIds(categories);
        if (categoryIds.Count > 0)
            query = query.Where(p => categoryIds.Contains(p.CategoryId));

        // FR-04: case-insensitive keyword match on name or description.
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                (p.Description != null && EF.Functions.ILike(p.Description, pattern)));
        }

        // FR-03: sort by name, or by distance (default).
        query = sort?.ToLowerInvariant() == "name"
            ? query.OrderBy(p => p.Name)
            : query.OrderBy(p => p.DistanceKm).ThenBy(p => p.Name);

        // FR-01: name, category, distance and a representative photo.
        var rows = await query
            .Select(p => new
            {
                p.Id,
                p.Name,
                Category = p.Category!.Name,
                p.DistanceKm,
                PhotoUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault(),
                p.EntryFee,
            })
            .ToListAsync();

        var items = rows
            .Select(r => new PlaceListItemDto(r.Id, r.Name, r.Category, r.DistanceKm, r.PhotoUrl, FormatEntryFee(r.EntryFee)))
            .ToList();

        // FR-05: number of places matching the filter and search.
        return new PlaceListResponseDto(items.Count, items);
    }

    public async Task<PlaceDetailDto?> GetPlaceAsync(int id)
    {
        Place? place = await _db.Places
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.OpeningHours)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsPublished); // FR-07

        if (place == null)
            return null;

        // FR-09: "today" and "now" are in Sri Lanka time, not server time.
        DateTime colomboNow = TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), GetColomboTimeZone()).DateTime;
        OpeningHour? today = place.OpeningHours.FirstOrDefault(o => o.DayOfWeek == colomboNow.DayOfWeek);

        // FR-44: one row per day, listed Monday to Sunday.
        var openingHours = place.OpeningHours
            .OrderBy(o => ((int)o.DayOfWeek + 6) % 7)
            .Select(o => new OpeningHourDto(
                o.DayOfWeek.ToString(),
                FormatOpenTime(o, o.OpenTime),
                FormatOpenTime(o, o.CloseTime),
                o.IsClosedAllDay))
            .ToList();

        // FR-11: photos in their display order, at most five.
        var images = place.Images
            .OrderBy(i => i.SortOrder)
            .Take(5)
            .Select(i => new PlaceImageDto(i.ImageUrl, i.Caption))
            .ToList();

        // FR-08, FR-10, FR-12, FR-13
        return new PlaceDetailDto(
            place.Id,
            place.Name,
            place.Category?.Name ?? NotAvailable,
            TextOrNotAvailable(place.Description),
            place.DistanceKm,
            place.Latitude,
            place.Longitude,
            place.EntryFee,
            FormatEntryFee(place.EntryFee),
            TextOrNotAvailable(place.TravelTips),
            today == null ? NotAvailable : FormatOpenTime(today, today.OpenTime),
            today == null ? NotAvailable : FormatOpenTime(today, today.CloseTime),
            IsOpenAt(today, TimeOnly.FromDateTime(colomboNow)),
            openingHours,
            images,
            place.UpdatedAt);
    }

    // FR-09: open if today is not closed and now is between opening and closing time.
    private static bool IsOpenAt(OpeningHour? today, TimeOnly now)
    {
        if (today == null || today.IsClosedAllDay || today.OpenTime == null || today.CloseTime == null)
            return false;

        return now >= today.OpenTime.Value && now < today.CloseTime.Value;
    }

    // FR-10: zero fee is shown as "Free entry".
    private static string FormatEntryFee(decimal fee)
    {
        return fee == 0
            ? FreeEntry
            : "LKR " + fee.ToString("#,0.##", CultureInfo.InvariantCulture);
    }

    // 24-hour "HH:mm" (SRS section 1), "Closed" for closed days, FR-12 if missing.
    private static string FormatOpenTime(OpeningHour hour, TimeOnly? time)
    {
        if (hour.IsClosedAllDay)
            return Closed;

        return time?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? NotAvailable;
    }

    private static string TextOrNotAvailable(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? NotAvailable : text;
    }

    // Turns "1,2,abc" into [1, 2]; anything that is not a number is ignored.
    private static List<int> ParseIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new List<int>();

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out int n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();
    }

    // "Asia/Colombo" is the IANA id; Windows without ICU may only know the Windows id.
    private static TimeZoneInfo GetColomboTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
        }
    }
}
