using System.Globalization;
using Microsoft.Extensions.Options;
using YaalTrail.Api.Configuration;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;

namespace YaalTrail.Api.Services;

// One place as the planner needs it. Built from the database by PlanService,
// or by hand in a unit test.
public record PlanPlace(
    int Id,
    string Name,
    string Category,
    decimal Latitude,
    decimal Longitude,
    List<OpeningHour> OpeningHours);

public record PlanInput(
    TimeOnly StartTime,
    DateOnly PlanDate,
    List<PlanPlace> Places,
    List<int>? ManualOrder = null,
    Dictionary<int, int>? DurationOverrides = null);

public interface IPlanBuilderService
{
    PlanResponseDto Build(PlanInput input);
}

// The day-plan algorithm (SRS 4.4). Pure logic: no database access,
// so it can be unit tested with hand-made places (NFR-23).
// NFR-03: nearest-neighbour is O(n^2); for 10 places this takes microseconds.
public class PlanBuilderService : IPlanBuilderService
{
    public const int DefaultVisitMinutes = 60;
    public const int BeachVisitMinutes = 90;
    public const int MaxVisitMinutes = 12 * 60;

    // FR-27: default visit duration per category, in minutes.
    private static readonly Dictionary<string, int> CategoryVisitMinutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Religious"] = 45,
        ["Heritage"] = 60,
        ["Natural"] = 60,
        ["Recreational"] = 60,
        ["Dining"] = 45,
        ["Public"] = 30
    };

    private readonly IDistanceService _distance;
    private readonly IOpeningHoursValidator _hoursValidator;
    private readonly PlannerOptions _options;
    private readonly int _latestArrivalMinutes;

    public PlanBuilderService(
        IDistanceService distance,
        IOpeningHoursValidator hoursValidator,
        IOptions<PlannerOptions> options)
    {
        _distance = distance;
        _hoursValidator = hoursValidator;
        _options = options.Value;

        if (_options.AverageSpeedKmh <= 0)
            throw new InvalidOperationException("Planner:AverageSpeedKmh must be greater than 0.");

        if (!TimeOnly.TryParseExact(_options.LatestArrival, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out TimeOnly latest))
            throw new InvalidOperationException("Planner:LatestArrival must be a time in HH:mm format.");

        _latestArrivalMinutes = ToMinutes(latest);
    }

    public PlanResponseDto Build(PlanInput input)
    {
        bool isManual = input.ManualOrder is { Count: > 0 };

        // FR-25: the visitor's own order, otherwise FR-24: nearest-neighbour.
        List<PlanPlace> ordered = isManual
            ? ApplyManualOrder(input.Places, input.ManualOrder!)
            : OrderByNearestNeighbour(input.Places);

        double referenceLat = (double)_options.ReferenceLatitude;
        double referenceLon = (double)_options.ReferenceLongitude;
        DayOfWeek day = input.PlanDate.DayOfWeek;

        // All times are "minutes since midnight", so adding never wraps around.
        int startMinutes = ToMinutes(input.StartTime);
        int clock = startMinutes;

        // Where the previous stop on the route is, and where the last scheduled stop is.
        double previousLat = referenceLat, previousLon = referenceLon;
        double lastScheduledLat = referenceLat, lastScheduledLon = referenceLon;
        int lastScheduledDeparture = startMinutes;

        double totalKm = 0;
        int totalTravel = 0, totalVisit = 0, scheduledCount = 0;
        var stops = new List<PlanStopDto>();

        for (int i = 0; i < ordered.Count; i++)
        {
            PlanPlace place = ordered[i];
            double lat = (double)place.Latitude, lon = (double)place.Longitude;

            // FR-26: travel time = road distance / average speed.
            double km = _distance.RoadKm(previousLat, previousLon, lat, lon);
            int travel = TravelMinutes(km);
            int visit = GetVisitMinutes(place, input.DurationOverrides);

            // FR-28: arrival = previous departure + travel; departure = arrival + visit.
            int arrival = clock + travel;
            int departure = arrival + visit;

            // NFR-10: never schedule an arrival after the latest arrival time (18:00).
            // Times only grow along the route, so every later stop is also unscheduled.
            bool isScheduled = arrival <= _latestArrivalMinutes;

            var warnings = new List<PlanWarningDto>();
            if (isScheduled)
            {
                warnings.AddRange(TimingWarnings(place, day, arrival));

                totalKm += km;
                totalTravel += travel;
                totalVisit += visit;
                scheduledCount++;
                lastScheduledLat = lat;
                lastScheduledLon = lon;
                lastScheduledDeparture = departure;
            }
            else
            {
                warnings.Add(new PlanWarningDto("NotScheduled",
                    $"Estimated arrival {FormatClock(arrival)} is after the latest arrival time " +
                    $"{FormatClock(_latestArrivalMinutes)}, so this place was not scheduled. " +
                    "Start earlier or remove places."));
            }

            stops.Add(new PlanStopDto(
                i + 1,
                place.Id,
                place.Name,
                place.Category,
                place.Latitude,
                place.Longitude,
                Math.Round(km, 1),
                travel,
                visit,
                isScheduled ? FormatClock(arrival) : null,
                isScheduled ? FormatClock(departure) : null,
                isScheduled,
                warnings));

            clock = departure;
            previousLat = lat;
            previousLon = lon;
        }

        // FR-24: end at the reference location, from the last scheduled stop.
        double returnKm = _distance.RoadKm(lastScheduledLat, lastScheduledLon, referenceLat, referenceLon);
        int returnTravel = TravelMinutes(returnKm);
        int backAtReference = lastScheduledDeparture + returnTravel;

        totalKm += returnKm;
        totalTravel += returnTravel;

        // FR-32
        var totals = new PlanTotalsDto(
            Math.Round(totalKm, 1),
            totalTravel,
            totalVisit,
            backAtReference - startMinutes,
            FormatClock(backAtReference),
            scheduledCount,
            ordered.Count - scheduledCount);

        return new PlanResponseDto(
            input.PlanDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            day.ToString(),
            FormatClock(startMinutes),
            isManual,
            stops,
            new PlanReturnLegDto(Math.Round(returnKm, 1), returnTravel, FormatClock(backAtReference)),
            totals,
            GetAdvisory(ordered.Count));
    }

    // FR-24: start at the reference location, always go to the closest unvisited
    // place (straight-line distance), then return to the reference location.
    public List<PlanPlace> OrderByNearestNeighbour(List<PlanPlace> places)
    {
        var remaining = places.OrderBy(p => p.Id).ToList(); // ties go to the lowest id
        var ordered = new List<PlanPlace>();

        double currentLat = (double)_options.ReferenceLatitude;
        double currentLon = (double)_options.ReferenceLongitude;

        while (remaining.Count > 0)
        {
            PlanPlace nearest = remaining[0];
            double nearestKm = double.MaxValue;

            foreach (PlanPlace candidate in remaining)
            {
                double km = _distance.StraightLineKm(
                    currentLat, currentLon, (double)candidate.Latitude, (double)candidate.Longitude);

                if (km < nearestKm)
                {
                    nearest = candidate;
                    nearestKm = km;
                }
            }

            ordered.Add(nearest);
            remaining.Remove(nearest);
            currentLat = (double)nearest.Latitude;
            currentLon = (double)nearest.Longitude;
        }

        return ordered;
    }

    // FR-25: the manual order must list every place in the plan exactly once.
    private static List<PlanPlace> ApplyManualOrder(List<PlanPlace> places, List<int> manualOrder)
    {
        bool sameIds = manualOrder.Count == places.Count &&
                       manualOrder.Distinct().Count() == manualOrder.Count &&
                       manualOrder.All(id => places.Any(p => p.Id == id));

        if (!sameIds)
            throw new PlanRequestException("Manual order must contain every place in the plan exactly once.");

        return manualOrder.Select(id => places.First(p => p.Id == id)).ToList();
    }

    // FR-27: the visitor's own duration if given, otherwise the category default.
    private static int GetVisitMinutes(PlanPlace place, Dictionary<int, int>? overrides)
    {
        if (overrides != null && overrides.TryGetValue(place.Id, out int minutes))
        {
            if (minutes < 1 || minutes > MaxVisitMinutes)
                throw new PlanRequestException(
                    $"Visit duration for '{place.Name}' must be between 1 and {MaxVisitMinutes} minutes.");
            return minutes;
        }

        // There is no "beach" category, so a Natural place with "beach" in its name counts as a beach.
        bool isBeach = string.Equals(place.Category, "Natural", StringComparison.OrdinalIgnoreCase) &&
                       place.Name.Contains("beach", StringComparison.OrdinalIgnoreCase);
        if (isBeach)
            return BeachVisitMinutes;

        return CategoryVisitMinutes.TryGetValue(place.Category, out int byCategory)
            ? byCategory
            : DefaultVisitMinutes;
    }

    // FR-29, FR-30: compare the arrival with the opening hours of the plan's day.
    private IEnumerable<PlanWarningDto> TimingWarnings(PlanPlace place, DayOfWeek day, int arrivalMinutes)
    {
        TimeOnly arrival = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(arrivalMinutes));
        OpeningHour? row = place.OpeningHours.FirstOrDefault(h => h.DayOfWeek == day);

        switch (_hoursValidator.Check(place.OpeningHours, day, arrival))
        {
            case OpeningHoursCheck.ArrivesAfterClosing:
                yield return new PlanWarningDto("ArrivesAfterClosing",
                    $"Arrives at {FormatClock(arrivalMinutes)}, after closing time {FormatTime(row!.CloseTime!.Value)}.");
                break;
            case OpeningHoursCheck.ArrivesBeforeOpening:
                yield return new PlanWarningDto("ArrivesBeforeOpening",
                    $"Arrives at {FormatClock(arrivalMinutes)}, before opening time {FormatTime(row!.OpenTime!.Value)}.");
                break;
            case OpeningHoursCheck.ClosedAllDay:
                yield return new PlanWarningDto("ClosedAllDay", $"Closed all day on {day}.");
                break;
            case OpeningHoursCheck.HoursNotRecorded:
                yield return new PlanWarningDto("HoursNotRecorded", $"Opening hours for {day} are not recorded.");
                break;
        }
    }

    // FR-31, BR-06
    private string? GetAdvisory(int placeCount)
    {
        if (placeCount <= _options.MaxRecommendedPlaces)
            return null;

        return $"Your plan has {placeCount} places. A realistic one-day plan has about " +
               $"{_options.MaxRecommendedPlaces} places or fewer; consider removing some.";
    }

    // FR-26: minutes = km / (km/h) * 60, rounded to whole minutes.
    private int TravelMinutes(double roadKm)
    {
        return (int)Math.Round(roadKm / (double)_options.AverageSpeedKmh * 60, MidpointRounding.AwayFromZero);
    }

    private static int ToMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;

    // 24-hour "HH:mm" (SRS section 1). Past midnight is marked, it should not normally happen.
    private static string FormatClock(int minutes)
    {
        string text = $"{minutes / 60 % 24:00}:{minutes % 60:00}";
        return minutes >= 24 * 60 ? text + " (next day)" : text;
    }

    private static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
