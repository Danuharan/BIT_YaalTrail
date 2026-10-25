using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// SRS 4.4 day planner: FR-24 to FR-32, NFR-10, BR-06.
// Most tests use StubDistanceService (every leg is 15 km of road = 30 minutes at 30 km/h)
// so the expected times can be worked out by hand.
public class PlanBuilderServiceTests
{
    private const double FifteenKm = 15; // 15 km / 30 km/h = 30 minutes

    private static PlanBuilderService Builder(double roadKm = FifteenKm) =>
        new(new StubDistanceService(roadKm), new OpeningHoursValidator(), TestData.Planner());

    // Real distances, for the nearest-neighbour tests.
    private static PlanBuilderService RealBuilder() =>
        new(new DistanceService(TestData.Planner()), new OpeningHoursValidator(), TestData.Planner());

    // A place open 06:00-20:00 every day unless other hours are given.
    private static PlanPlace Place(int id, string category = "Religious", string? name = null,
        List<OpeningHour>? hours = null, decimal latOffset = 0)
    {
        return new PlanPlace(id, name ?? $"Place {id}", category,
            TestData.ReferenceLat + latOffset, TestData.ReferenceLon, hours ?? TestData.EveryDay(6, 20));
    }

    private static PlanInput Input(string start, List<PlanPlace> places,
        List<int>? manualOrder = null, Dictionary<int, int>? overrides = null)
    {
        return new PlanInput(TimeOnly.Parse(start), TestData.Monday, places, manualOrder, overrides);
    }

    // ---------- FR-24 ordering ----------

    // A is 1 km north, B 2 km south, C 3 km north. From Chundikuli the nearest is A;
    // from A, C (2 km) is nearer than B (3 km); then B. Sorting by distance from
    // Chundikuli alone would give A, B, C, so this checks the greedy step.
    [Fact]
    public void Build_NoManualOrder_VisitsNearestUnvisitedPlaceEachTime()
    {
        var places = new List<PlanPlace>
        {
            Place(1, latOffset: 0.009m),   // A
            Place(2, latOffset: -0.018m),  // B
            Place(3, latOffset: 0.027m),   // C
        };

        PlanResponseDto plan = RealBuilder().Build(Input("08:00", places));

        Assert.Equal(new[] { 1, 3, 2 }, plan.Stops.Select(s => s.PlaceId));
        Assert.False(plan.IsManualOrder);
    }

    [Fact]
    public void Build_PlacesEquallyFar_LowestIdGoesFirst()
    {
        var places = new List<PlanPlace> { Place(3), Place(1), Place(2) };

        PlanResponseDto plan = Builder().Build(Input("08:00", places));

        Assert.Equal(new[] { 1, 2, 3 }, plan.Stops.Select(s => s.PlaceId));
    }

    [Fact]
    public void OrderByNearestNeighbour_Places_StartsWithClosestToReference()
    {
        var places = new List<PlanPlace> { Place(1, latOffset: 0.05m), Place(2, latOffset: 0.01m), Place(3, latOffset: 0.03m) };

        List<PlanPlace> ordered = RealBuilder().OrderByNearestNeighbour(places);

        Assert.Equal(new[] { 2, 3, 1 }, ordered.Select(p => p.Id));
    }

    // ---------- FR-25 manual order ----------

    [Fact]
    public void Build_ValidManualOrder_KeepsVisitorOrder()
    {
        var places = new List<PlanPlace> { Place(1), Place(2), Place(3) };

        PlanResponseDto plan = Builder().Build(Input("08:00", places, manualOrder: new List<int> { 3, 1, 2 }));

        Assert.Equal(new[] { 3, 1, 2 }, plan.Stops.Select(s => s.PlaceId));
        Assert.Equal(new[] { 1, 2, 3 }, plan.Stops.Select(s => s.Order));
        Assert.True(plan.IsManualOrder);
    }

    public static TheoryData<List<int>> InvalidManualOrders => new()
    {
        new List<int> { 1, 2 },        // a place is missing
        new List<int> { 1, 1, 2 },     // a place twice
        new List<int> { 1, 2, 99 },    // a place that is not in the plan
        new List<int> { 1, 2, 3, 3 },  // too many
    };

    [Theory]
    [MemberData(nameof(InvalidManualOrders))]
    public void Build_InvalidManualOrder_ThrowsPlanRequestException(List<int> manualOrder)
    {
        var places = new List<PlanPlace> { Place(1), Place(2), Place(3) };

        var error = Assert.Throws<PlanRequestException>(() =>
            Builder().Build(Input("08:00", places, manualOrder: manualOrder)));
        Assert.Contains("exactly once", error.Message);
    }

    [Fact]
    public void Build_EmptyManualOrder_UsesNearestNeighbour()
    {
        var places = new List<PlanPlace> { Place(2), Place(1) };

        PlanResponseDto plan = Builder().Build(Input("08:00", places, manualOrder: new List<int>()));

        Assert.False(plan.IsManualOrder);
        Assert.Equal(new[] { 1, 2 }, plan.Stops.Select(s => s.PlaceId));
    }

    // ---------- FR-27 visit durations ----------

    [Theory]
    [InlineData("Religious", "Nallur Kovil", 45)]
    [InlineData("Heritage", "Jaffna Fort", 60)]
    [InlineData("Natural", "Keerimalai Spring", 60)]
    [InlineData("Natural", "Casuarina Beach", 90)]     // a Natural place named "beach"
    [InlineData("Recreational", "Old Park", 60)]
    [InlineData("Recreational", "Beach Road Park", 60)] // "beach" only counts for Natural
    [InlineData("Dining", "Mangos Restaurant", 45)]
    [InlineData("Public", "Central Market", 30)]
    [InlineData("Shopping", "Unknown Category Place", 60)] // no default for the category
    public void Build_NoOverride_UsesDefaultVisitMinutesForCategory(string category, string name, int expectedMinutes)
    {
        var places = new List<PlanPlace> { Place(1, category, name) };

        PlanResponseDto plan = Builder().Build(Input("08:00", places));

        Assert.Equal(expectedMinutes, plan.Stops[0].VisitMinutes);
    }

    [Fact]
    public void Build_DurationOverride_ReplacesDefaultAndMovesLaterTimes()
    {
        var places = new List<PlanPlace> { Place(1), Place(2) };
        var overrides = new Dictionary<int, int> { [1] = 120 };

        PlanResponseDto plan = Builder().Build(Input("08:00", places, overrides: overrides));

        Assert.Equal(120, plan.Stops[0].VisitMinutes);
        Assert.Equal("08:30", plan.Stops[0].Arrival);
        Assert.Equal("10:30", plan.Stops[0].Departure);
        Assert.Equal("11:00", plan.Stops[1].Arrival);   // 10:30 + 30 min travel
        Assert.Equal(45, plan.Stops[1].VisitMinutes);   // no override: Religious default
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(721)]
    public void Build_DurationOverrideOutOfRange_ThrowsPlanRequestException(int minutes)
    {
        var places = new List<PlanPlace> { Place(1) };
        var overrides = new Dictionary<int, int> { [1] = minutes };

        Assert.Throws<PlanRequestException>(() => Builder().Build(Input("08:00", places, overrides: overrides)));
    }

    // ---------- FR-26, FR-28 times ----------

    // Start 08:00, two Religious places (45 min each), every leg 15 km = 30 min.
    // Stop 1: arrive 08:30, leave 09:15. Stop 2: arrive 09:45, leave 10:30. Back 11:00.
    [Fact]
    public void Build_TwoPlaces_ArrivalIsPreviousDeparturePlusTravelAndDepartureAddsVisit()
    {
        var places = new List<PlanPlace> { Place(1), Place(2) };

        PlanResponseDto plan = Builder().Build(Input("08:00", places));

        Assert.Equal("08:30", plan.Stops[0].Arrival);
        Assert.Equal("09:15", plan.Stops[0].Departure);
        Assert.Equal("09:45", plan.Stops[1].Arrival);
        Assert.Equal("10:30", plan.Stops[1].Departure);
        Assert.Equal("11:00", plan.ReturnLeg.ArrivalBack);
        Assert.Equal("08:00", plan.StartTime);
        Assert.Equal("Monday", plan.Day);
        Assert.Equal("2026-10-26", plan.PlanDate);
    }

    // FR-26: minutes = road km / 30 km/h * 60, rounded to whole minutes (halves round up).
    [Theory]
    [InlineData(10, 20)]
    [InlineData(5, 10)]
    [InlineData(7.5, 15)]
    [InlineData(0.75, 2)]   // 1.5 min rounds up to 2
    [InlineData(0.24, 0)]   // 0.48 min rounds down to 0
    public void Build_RoadDistance_TravelMinutesIsDistanceOverAverageSpeed(double roadKm, int expectedMinutes)
    {
        PlanResponseDto plan = Builder(roadKm).Build(Input("08:00", new List<PlanPlace> { Place(1) }));

        Assert.Equal(expectedMinutes, plan.Stops[0].TravelMinutesFromPrevious);
        Assert.Equal(expectedMinutes, plan.ReturnLeg.TravelMinutes);
    }

    [Fact]
    public void Build_DifferentAverageSpeed_ChangesTravelTime()
    {
        var builder = new PlanBuilderService(new StubDistanceService(FifteenKm), new OpeningHoursValidator(),
            TestData.Planner(o => o.AverageSpeedKmh = 45));

        PlanResponseDto plan = builder.Build(Input("08:00", new List<PlanPlace> { Place(1) }));

        Assert.Equal(20, plan.Stops[0].TravelMinutesFromPrevious); // 15 km / 45 km/h = 20 min
    }

    // ---------- FR-32 totals ----------

    [Fact]
    public void Build_TwoPlaces_TotalsIncludeReturnLeg()
    {
        var places = new List<PlanPlace> { Place(1), Place(2) };

        PlanTotalsDto totals = Builder().Build(Input("08:00", places)).Totals;

        Assert.Equal(45, totals.TotalTravelDistanceKm);   // 15 + 15 + 15 back
        Assert.Equal(90, totals.TotalTravelMinutes);      // 30 + 30 + 30 back
        Assert.Equal(90, totals.TotalVisitMinutes);       // 45 + 45
        Assert.Equal(180, totals.TotalElapsedMinutes);    // 08:00 to 11:00
        Assert.Equal("11:00", totals.EndTime);
        Assert.Equal(2, totals.ScheduledCount);
        Assert.Equal(0, totals.UnscheduledCount);
    }

    [Fact]
    public void Build_NoPlaces_ReturnsEmptyPlanEndingAtStartTime()
    {
        // Real distances: the "return leg" is Chundikuli to Chundikuli, 0 km.
        PlanResponseDto plan = RealBuilder().Build(Input("09:15", new List<PlanPlace>()));

        Assert.Empty(plan.Stops);
        Assert.Equal(0, plan.Totals.TotalTravelMinutes);
        Assert.Equal(0, plan.Totals.TotalElapsedMinutes);
        Assert.Equal("09:15", plan.Totals.EndTime);
        Assert.Null(plan.Advisory);
    }

    // ---------- FR-29, FR-30 timing warnings ----------

    [Fact]
    public void Build_ArrivalBeforeOpening_AddsArrivesBeforeOpeningWarning()
    {
        var places = new List<PlanPlace> { Place(1, hours: TestData.EveryDay(9, 17)) };

        PlanStopDto stop = Builder().Build(Input("08:00", places)).Stops[0]; // arrives 08:30

        PlanWarningDto warning = Assert.Single(stop.Warnings);
        Assert.Equal("ArrivesBeforeOpening", warning.Type);
        Assert.Contains("08:30", warning.Text);
        Assert.Contains("09:00", warning.Text);
        Assert.True(stop.IsScheduled); // a warning does not remove the stop
    }

    [Fact]
    public void Build_ArrivalAfterClosing_AddsArrivesAfterClosingWarning()
    {
        var places = new List<PlanPlace> { Place(1, hours: TestData.EveryDay(6, 8)) };

        PlanStopDto stop = Builder().Build(Input("08:00", places)).Stops[0]; // arrives 08:30, closed 08:00

        PlanWarningDto warning = Assert.Single(stop.Warnings);
        Assert.Equal("ArrivesAfterClosing", warning.Type);
        Assert.Contains("08:30", warning.Text);
        Assert.Contains("08:00", warning.Text);
    }

    [Fact]
    public void Build_PlaceClosedOnPlanDay_AddsClosedAllDayWarning()
    {
        var hours = new List<OpeningHour> { new() { DayOfWeek = DayOfWeek.Monday, IsClosedAllDay = true } };

        PlanStopDto stop = Builder().Build(Input("08:00", new List<PlanPlace> { Place(1, hours: hours) })).Stops[0];

        Assert.Equal("ClosedAllDay", Assert.Single(stop.Warnings).Type);
    }

    [Fact]
    public void Build_NoHoursForPlanDay_AddsHoursNotRecordedWarning()
    {
        PlanStopDto stop = Builder().Build(Input("08:00", new List<PlanPlace> { Place(1, hours: new List<OpeningHour>()) })).Stops[0];

        Assert.Equal("HoursNotRecorded", Assert.Single(stop.Warnings).Type);
    }

    [Fact]
    public void Build_ArrivalWithinHours_HasNoWarnings()
    {
        PlanStopDto stop = Builder().Build(Input("08:00", new List<PlanPlace> { Place(1) })).Stops[0];

        Assert.Empty(stop.Warnings);
    }

    // ---------- FR-31, BR-06 advisory ----------

    [Fact]
    public void Build_SevenPlaces_ReturnsAdvisory()
    {
        var places = Enumerable.Range(1, 7).Select(id => Place(id)).ToList();

        PlanResponseDto plan = Builder(roadKm: 1).Build(Input("08:00", places));

        Assert.NotNull(plan.Advisory);
        Assert.Contains("7 places", plan.Advisory);
    }

    [Fact]
    public void Build_SixPlaces_HasNoAdvisory()
    {
        var places = Enumerable.Range(1, 6).Select(id => Place(id)).ToList();

        PlanResponseDto plan = Builder(roadKm: 1).Build(Input("08:00", places));

        Assert.Null(plan.Advisory);
    }

    // ---------- NFR-10 latest arrival 18:00 ----------

    // Start 17:00. Stop 1 arrives 17:30 and stays 60 min (leaves 18:30).
    // Stop 2 would arrive 19:00, after 18:00, so it is not scheduled.
    [Fact]
    public void Build_ArrivalAfterLatestArrival_StopIsNotScheduledAndHasNoTimes()
    {
        var places = new List<PlanPlace> { Place(1), Place(2) };
        var overrides = new Dictionary<int, int> { [1] = 60 };

        PlanResponseDto plan = Builder().Build(Input("17:00", places, overrides: overrides));

        PlanStopDto late = plan.Stops[1];
        Assert.False(late.IsScheduled);
        Assert.Null(late.Arrival);
        Assert.Null(late.Departure);

        PlanWarningDto warning = Assert.Single(late.Warnings);
        Assert.Equal("NotScheduled", warning.Type);
        Assert.Contains("19:00", warning.Text); // the estimated arrival
        Assert.Contains("18:00", warning.Text); // the limit
    }

    [Fact]
    public void Build_UnscheduledStop_IsExcludedFromTotals()
    {
        var places = new List<PlanPlace> { Place(1), Place(2) };
        var overrides = new Dictionary<int, int> { [1] = 60 };

        PlanTotalsDto totals = Builder().Build(Input("17:00", places, overrides: overrides)).Totals;

        Assert.Equal(1, totals.ScheduledCount);
        Assert.Equal(1, totals.UnscheduledCount);
        Assert.Equal(30, totals.TotalTravelDistanceKm);  // to stop 1 and back, not to stop 2
        Assert.Equal(60, totals.TotalTravelMinutes);
        Assert.Equal(60, totals.TotalVisitMinutes);
        Assert.Equal("19:00", totals.EndTime);           // leave stop 1 at 18:30 + 30 min home
        Assert.Equal(120, totals.TotalElapsedMinutes);
    }

    [Fact]
    public void Build_ArrivalExactlyAtLatestArrival_IsScheduled()
    {
        PlanStopDto stop = Builder().Build(Input("17:30", new List<PlanPlace> { Place(1) })).Stops[0];

        Assert.True(stop.IsScheduled);
        Assert.Equal("18:00", stop.Arrival);
    }

    [Fact]
    public void Build_StopAfterAnUnscheduledStop_IsAlsoUnscheduled()
    {
        var places = new List<PlanPlace> { Place(1), Place(2), Place(3) };

        PlanResponseDto plan = Builder().Build(Input("17:40", places)); // stop 1 arrives 18:10

        Assert.All(plan.Stops, stop => Assert.False(stop.IsScheduled));
        Assert.Equal(0, plan.Totals.ScheduledCount);
        Assert.Equal(3, plan.Totals.UnscheduledCount);
        Assert.Equal(0, plan.Totals.TotalVisitMinutes);
    }

    // ---------- configuration checks ----------

    [Fact]
    public void Constructor_ZeroAverageSpeed_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new PlanBuilderService(
            new StubDistanceService(1), new OpeningHoursValidator(), TestData.Planner(o => o.AverageSpeedKmh = 0)));
    }

    [Fact]
    public void Constructor_LatestArrivalNotHHmm_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new PlanBuilderService(
            new StubDistanceService(1), new OpeningHoursValidator(), TestData.Planner(o => o.LatestArrival = "6pm")));
    }
}
