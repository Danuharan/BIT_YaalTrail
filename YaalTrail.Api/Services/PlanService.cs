using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;

namespace YaalTrail.Api.Services;

public interface IPlanService
{
    Task<PlanResponseDto> CalculateAsync(PlanRequestDto request);
}

// Reads the request and loads the places from the database,
// then hands everything to PlanBuilderService, which does the calculation.
public class PlanService : IPlanService
{
    // FR-23: default start time.
    public static readonly TimeOnly DefaultStartTime = new(8, 0);

    private readonly AppDbContext _db;
    private readonly IPlanBuilderService _builder;
    private readonly TimeProvider _clock;

    public PlanService(AppDbContext db, IPlanBuilderService builder, TimeProvider clock)
    {
        _db = db;
        _builder = builder;
        _clock = clock;
    }

    public async Task<PlanResponseDto> CalculateAsync(PlanRequestDto request)
    {
        TimeOnly startTime = ParseStartTime(request.StartTime);
        DateOnly planDate = ParsePlanDate(request.PlanDate);
        List<int> placeIds = (request.PlaceIds ?? new List<int>()).Distinct().ToList();

        // NFR-02: one SQL query (JOINs) for the places, categories and opening hours.
        // FR-07: only published places can be planned.
        List<Place> rows = await _db.Places
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.OpeningHours)
            .AsSingleQuery()
            .Where(p => p.IsPublished && placeIds.Contains(p.Id))
            .ToListAsync();

        List<int> missing = placeIds.Except(rows.Select(p => p.Id)).ToList();
        if (missing.Count > 0)
            throw new PlanRequestException("Unknown or unpublished place id(s): " + string.Join(", ", missing));

        var places = rows
            .Select(p => new PlanPlace(
                p.Id,
                p.Name,
                p.Category?.Name ?? string.Empty,
                p.Latitude,
                p.Longitude,
                p.OpeningHours))
            .ToList();

        return _builder.Build(new PlanInput(
            startTime,
            planDate,
            places,
            request.ManualOrder,
            request.DurationOverrides));
    }

    // FR-23: "HH:mm", 08:00 when not given.
    private static TimeOnly ParseStartTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return DefaultStartTime;

        if (!TimeOnly.TryParseExact(text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
            throw new PlanRequestException("Start time must be in HH:mm format, for example 08:00.");

        return time;
    }

    // BR-03: a plan is for one calendar day; today (Sri Lanka time) when not given.
    private DateOnly ParsePlanDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return DateOnly.FromDateTime(SriLankaTime.Now(_clock));

        if (!DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            throw new PlanRequestException("Plan date must be in yyyy-MM-dd format.");

        return date;
    }
}
