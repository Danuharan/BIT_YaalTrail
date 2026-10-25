using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YaalTrail.Api.Configuration;
using YaalTrail.Api.Data;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// Shared helpers for the unit tests (NFR-23).

// A clock the test controls, so lockout, idle and "open now" rules can be tested exactly.
public class FakeClock : TimeProvider
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset start)
    {
        _now = start;
    }

    // 2026-10-26 is a Monday. 02:30 UTC = 08:00 in Sri Lanka (UTC+05:30).
    public static FakeClock MondayMorning() => new(new DateTimeOffset(2026, 10, 26, 2, 30, 0, TimeSpan.Zero));

    public DateTime UtcNow => _now.UtcDateTime;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

// Returns fixed distances so travel times can be worked out by hand in the tests.
public class StubDistanceService : IDistanceService
{
    private readonly double _roadKm;

    public StubDistanceService(double roadKm)
    {
        _roadKm = roadKm;
    }

    // Every place is the same distance away, so nearest-neighbour keeps the lowest id first.
    public double StraightLineKm(double lat1, double lon1, double lat2, double lon2) => _roadKm;

    public double RoadKm(double lat1, double lon1, double lat2, double lon2) => _roadKm;
}

// Lets a test check that a service told the cache to forget the catalogue.
public class SpyCatalogueCache : ICatalogueCache
{
    public int InvalidateCount { get; private set; }

    public Task<T> GetOrLoadAsync<T>(string key, Func<Task<T>> load) => load();

    public void Invalidate() => InvalidateCount++;
}

public static class TestData
{
    // Chundikuli, the reference location (SRS 6.4).
    public const decimal ReferenceLat = 9.6548m;
    public const decimal ReferenceLon = 80.0307m;

    // Monday 26 October 2026.
    public static readonly DateOnly Monday = new(2026, 10, 26);

    // Same values as appsettings.json "Planner".
    public static PlannerOptions PlannerOptions() => new()
    {
        ReferenceLatitude = ReferenceLat,
        ReferenceLongitude = ReferenceLon,
        RadiusKm = 25,
        AverageSpeedKmh = 30,
        RoadFactor = 1.3m,
        MaxRecommendedPlaces = 6,
        LatestArrival = "18:00",
    };

    public static IOptions<PlannerOptions> Planner(Action<PlannerOptions>? change = null)
    {
        PlannerOptions options = PlannerOptions();
        change?.Invoke(options);
        return Options.Create(options);
    }

    // A fresh, empty in-memory database for each test.
    public static AppDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"yaaltrail-tests-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    // The same opening and closing time on all seven days.
    public static List<OpeningHour> EveryDay(int openHour, int closeHour) =>
        Enum.GetValues<DayOfWeek>()
            .Select(day => new OpeningHour
            {
                DayOfWeek = day,
                OpenTime = new TimeOnly(openHour, 0),
                CloseTime = new TimeOnly(closeHour, 0),
            })
            .ToList();
}
