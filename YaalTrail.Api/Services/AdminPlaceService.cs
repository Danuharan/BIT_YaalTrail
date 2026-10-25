using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YaalTrail.Api.Configuration;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;

namespace YaalTrail.Api.Services;

// SRS 4.6: place management for authenticated administrators (BR-02).
public interface IAdminPlaceService
{
    Task<List<AdminPlaceListItemDto>> GetAllAsync();
    Task<AdminPlaceDto?> GetAsync(int id);
    Task<AdminPlaceDto> CreateAsync(AdminPlaceRequestDto request, int adminId);
    Task<AdminPlaceDto?> UpdateAsync(int id, AdminPlaceRequestDto request, int adminId);
    Task<bool> DeleteAsync(int id, bool confirmed);
    Task<AdminPlaceDto?> SetPublishedAsync(int id, bool published, int adminId);
}

public class AdminPlaceService : IAdminPlaceService
{
    private static readonly string[] TimeFormats = { "HH:mm", "H:mm" };

    private readonly AppDbContext _db;
    private readonly ICatalogueCache _cache;
    private readonly IDistanceService _distance;
    private readonly TimeProvider _clock;
    private readonly PlannerOptions _planner;

    public AdminPlaceService(
        AppDbContext db,
        ICatalogueCache cache,
        IDistanceService distance,
        TimeProvider clock,
        IOptions<PlannerOptions> planner)
    {
        _db = db;
        _cache = cache;
        _distance = distance;
        _clock = clock;
        _planner = planner.Value;
    }

    // Unlike the visitor catalogue (FR-07), unpublished places are included.
    public async Task<List<AdminPlaceListItemDto>> GetAllAsync()
    {
        return await _db.Places
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new AdminPlaceListItemDto(
                p.Id,
                p.Name,
                p.CategoryId,
                p.Category!.Name,
                p.DistanceKm,
                p.IsPublished,
                p.UpdatedAt,
                p.MaintainedByAdmin != null ? p.MaintainedByAdmin.DisplayName : null))
            .ToListAsync();
    }

    public async Task<AdminPlaceDto?> GetAsync(int id)
    {
        Place? place = await _db.Places
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.MaintainedByAdmin)
            .Include(p => p.Images)
            .Include(p => p.OpeningHours)
            .AsSingleQuery()
            .FirstOrDefaultAsync(p => p.Id == id);

        return place == null ? null : await ToDtoAsync(place);
    }

    // FR-39
    public async Task<AdminPlaceDto> CreateAsync(AdminPlaceRequestDto request, int adminId)
    {
        List<OpeningHour> hours = await ValidateAsync(request);
        DateTime now = _clock.GetUtcNow().UtcDateTime;

        var place = new Place { CreatedAt = now }; // FR-48
        Apply(place, request, hours, now, adminId);

        _db.Places.Add(place);
        await _db.SaveChangesAsync();
        _cache.Invalidate();

        return (await GetAsync(place.Id))!;
    }

    // FR-40: any attribute can be changed. Opening hours are replaced as a whole;
    // photos are left as they are.
    public async Task<AdminPlaceDto?> UpdateAsync(int id, AdminPlaceRequestDto request, int adminId)
    {
        Place? place = await _db.Places
            .Include(p => p.OpeningHours)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (place == null)
            return null;

        List<OpeningHour> hours = await ValidateAsync(request);

        _db.OpeningHours.RemoveRange(place.OpeningHours);
        place.OpeningHours.Clear();
        Apply(place, request, hours, _clock.GetUtcNow().UtcDateTime, adminId);

        await _db.SaveChangesAsync();
        _cache.Invalidate();

        return await GetAsync(id);
    }

    // FR-41: deleting needs explicit confirmation (?confirm=true). Saved plans are kept:
    // PlanItem.PlaceId is set to NULL and the plan shows PlaceNameSnapshot instead.
    public async Task<bool> DeleteAsync(int id, bool confirmed)
    {
        Place? place = await _db.Places.FirstOrDefaultAsync(p => p.Id == id);
        if (place == null)
            return false;

        if (!confirmed)
        {
            int savedPlans = await CountSavedPlansAsync(id);
            string warning = savedPlans == 0
                ? "No saved plans use this place."
                : $"{savedPlans} saved plan(s) include this place; they will show it as unavailable.";
            throw new AdminRequestException($"Deleting a place must be confirmed with ?confirm=true. {warning}");
        }

        _db.Places.Remove(place); // photos and opening hours are deleted by cascade
        await _db.SaveChangesAsync();
        _cache.Invalidate();
        return true;
    }

    // FR-47
    public async Task<AdminPlaceDto?> SetPublishedAsync(int id, bool published, int adminId)
    {
        Place? place = await _db.Places.FirstOrDefaultAsync(p => p.Id == id);
        if (place == null)
            return null;

        // BR-01: check the radius again, in case the reference location was changed in settings.
        if (published)
        {
            string? radiusError = CheckRadius(place.Latitude, place.Longitude);
            if (radiusError != null)
                throw new AdminRequestException(radiusError);
        }

        place.IsPublished = published;
        place.UpdatedAt = _clock.GetUtcNow().UtcDateTime; // FR-48
        place.MaintainedByAdminId = adminId;

        await _db.SaveChangesAsync();
        _cache.Invalidate();

        return await GetAsync(id);
    }

    // Collects every problem with the request so the admin can fix them all at once.
    // Returns the opening hours turned into entities.
    private async Task<List<OpeningHour>> ValidateAsync(AdminPlaceRequestDto request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Name is required.");

        // BR-05: every place belongs to an existing category.
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId))
            errors.Add($"Category {request.CategoryId} does not exist; every place needs a category (BR-05).");

        // FR-42, BR-01
        string? radiusError = CheckRadius(request.Latitude, request.Longitude);
        if (radiusError != null)
            errors.Add(radiusError);

        List<OpeningHour> hours = ParseOpeningHours(request.OpeningHours, errors);

        if (errors.Count > 0)
            throw new AdminRequestException(errors);

        return hours;
    }

    // FR-42: straight-line (haversine) distance from the reference location, compared
    // with the permitted radius. Both come from the "Planner" settings (SRS 6.4).
    private string? CheckRadius(decimal latitude, decimal longitude)
    {
        double km = _distance.StraightLineKm(
            (double)_planner.ReferenceLatitude, (double)_planner.ReferenceLongitude,
            (double)latitude, (double)longitude);

        if (km <= (double)_planner.RadiusKm)
            return null;

        return string.Format(CultureInfo.InvariantCulture,
            "The coordinates are {0:0.0} km from the reference location ({1}, {2}); " +
            "only places within {3} km may be added (FR-42, BR-01).",
            km, _planner.ReferenceLatitude, _planner.ReferenceLongitude, _planner.RadiusKm);
    }

    // FR-43, FR-44: at most one entry per day; a day is either closed or has
    // an opening time and a later closing time. Days not sent are "not recorded" (FR-12).
    private static List<OpeningHour> ParseOpeningHours(List<AdminOpeningHourDto>? entries, List<string> errors)
    {
        var hours = new List<OpeningHour>();
        if (entries == null)
            return hours;

        foreach (AdminOpeningHourDto entry in entries)
        {
            // Accept day names only ("Monday"), not numbers.
            if (!Enum.TryParse(entry.Day, ignoreCase: true, out DayOfWeek day) ||
                !Enum.IsDefined(day) || int.TryParse(entry.Day, out _))
            {
                errors.Add($"'{entry.Day}' is not a day of the week.");
                continue;
            }

            if (hours.Any(h => h.DayOfWeek == day))
            {
                errors.Add($"{day} is listed more than once.");
                continue;
            }

            if (entry.IsClosed)
            {
                hours.Add(new OpeningHour { DayOfWeek = day, IsClosedAllDay = true });
                continue;
            }

            bool openOk = TryParseTime(entry.OpenTime, out TimeOnly open);
            bool closeOk = TryParseTime(entry.CloseTime, out TimeOnly close);

            if (!openOk || !closeOk)
            {
                errors.Add($"{day}: opening and closing times must be given as HH:mm, or mark the day as closed.");
                continue;
            }

            // FR-43
            if (close <= open)
            {
                errors.Add($"{day}: closing time {close:HH:mm} must be later than opening time {open:HH:mm}.");
                continue;
            }

            hours.Add(new OpeningHour { DayOfWeek = day, OpenTime = open, CloseTime = close });
        }

        return hours;
    }

    private static bool TryParseTime(string? text, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(text?.Trim(), TimeFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out time);
    }

    private void Apply(Place place, AdminPlaceRequestDto request, List<OpeningHour> hours, DateTime now, int adminId)
    {
        place.Name = request.Name.Trim();
        place.CategoryId = request.CategoryId;
        place.Description = EmptyToNull(request.Description);
        place.Latitude = request.Latitude;
        place.Longitude = request.Longitude;
        place.DistanceKm = request.DistanceKm ?? EstimateRoadKm(request.Latitude, request.Longitude);
        place.EntryFee = request.EntryFee;
        place.TravelTips = EmptyToNull(request.TravelTips);
        place.IsPublished = request.IsPublished;
        place.OpeningHours.AddRange(hours);

        // FR-48
        place.UpdatedAt = now;
        place.MaintainedByAdminId = adminId;
    }

    // FR-26, TBD-02: same road-distance estimate the planner uses.
    private decimal EstimateRoadKm(decimal latitude, decimal longitude)
    {
        double km = _distance.RoadKm(
            (double)_planner.ReferenceLatitude, (double)_planner.ReferenceLongitude,
            (double)latitude, (double)longitude);
        return Math.Round((decimal)km, 2);
    }

    private Task<int> CountSavedPlansAsync(int placeId)
    {
        return _db.PlanItems
            .Where(pi => pi.PlaceId == placeId)
            .Select(pi => pi.VisitPlanId)
            .Distinct()
            .CountAsync();
    }

    private async Task<AdminPlaceDto> ToDtoAsync(Place place)
    {
        var hours = place.OpeningHours
            .OrderBy(o => ((int)o.DayOfWeek + 6) % 7) // Monday first
            .Select(o => new AdminOpeningHourDto(
                o.DayOfWeek.ToString(),
                o.OpenTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
                o.CloseTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
                o.IsClosedAllDay))
            .ToList();

        var images = place.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new PlaceImageDto(i.ImageUrl, i.Caption))
            .ToList();

        return new AdminPlaceDto(
            place.Id,
            place.Name,
            place.CategoryId,
            place.Category?.Name ?? string.Empty,
            place.Description,
            place.Latitude,
            place.Longitude,
            place.DistanceKm,
            place.EntryFee,
            place.TravelTips,
            place.IsPublished,
            hours,
            images,
            place.CreatedAt,
            place.UpdatedAt,
            place.MaintainedByAdminId,
            place.MaintainedByAdmin?.DisplayName,
            await CountSavedPlansAsync(place.Id));
    }

    private static string? EmptyToNull(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
