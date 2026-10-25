using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// The database step of the planner: reading the request and loading places (FR-07, FR-23, BR-03).
public class PlanServiceTests
{
    private readonly AppDbContext _db = TestData.NewDatabase();
    private readonly PlanService _service;
    private readonly int _publishedId;
    private readonly int _draftId;

    public PlanServiceTests()
    {
        var builder = new PlanBuilderService(new DistanceService(TestData.Planner()), new OpeningHoursValidator(), TestData.Planner());
        _service = new PlanService(_db, builder, FakeClock.MondayMorning());

        var category = new Category { Name = "Religious" };
        var published = NewPlace("Nallur Kovil", category, true);
        var draft = NewPlace("Draft", category, false);
        _db.Places.AddRange(published, draft);
        _db.SaveChanges();
        _publishedId = published.Id;
        _draftId = draft.Id;
    }

    private static Place NewPlace(string name, Category category, bool published) => new()
    {
        Name = name, Category = category, IsPublished = published,
        Latitude = 9.674847m, Longitude = 80.029322m, OpeningHours = TestData.EveryDay(5, 20),
    };

    private static PlanRequestDto Request(List<int> ids, string? start = null, string? date = null) =>
        new(start, date, ids, null, null);

    [Fact]
    public async Task CalculateAsync_NoStartTimeOrDate_Uses0800AndTodayInSriLanka()
    {
        PlanResponseDto plan = await _service.CalculateAsync(Request(new List<int> { _publishedId }));

        Assert.Equal("08:00", plan.StartTime);
        Assert.Equal("2026-10-26", plan.PlanDate);
        Assert.Equal("Monday", plan.Day);
        Assert.Equal("Nallur Kovil", Assert.Single(plan.Stops).Name);
    }

    [Fact]
    public async Task CalculateAsync_GivenStartTimeAndDate_UsesThem()
    {
        PlanResponseDto plan = await _service.CalculateAsync(Request(new List<int> { _publishedId }, "09:30", "2026-11-01"));

        Assert.Equal("09:30", plan.StartTime);
        Assert.Equal("Sunday", plan.Day);
    }

    [Fact]
    public async Task CalculateAsync_SamePlaceTwice_PlansItOnce()
    {
        PlanResponseDto plan = await _service.CalculateAsync(Request(new List<int> { _publishedId, _publishedId }));

        Assert.Single(plan.Stops);
    }

    [Fact]
    public async Task CalculateAsync_NoPlaceIds_ReturnsEmptyPlan()
    {
        PlanResponseDto plan = await _service.CalculateAsync(new PlanRequestDto(null, null, null, null, null));

        Assert.Empty(plan.Stops);
    }

    // FR-07: unpublished places cannot be planned.
    [Fact]
    public async Task CalculateAsync_UnpublishedPlace_ThrowsWithItsId()
    {
        var error = await Assert.ThrowsAsync<PlanRequestException>(() =>
            _service.CalculateAsync(Request(new List<int> { _publishedId, _draftId })));

        Assert.Contains(_draftId.ToString(), error.Message);
    }

    [Fact]
    public async Task CalculateAsync_UnknownPlace_Throws()
    {
        await Assert.ThrowsAsync<PlanRequestException>(() => _service.CalculateAsync(Request(new List<int> { 999 })));
    }

    [Theory]
    [InlineData("8:00")]
    [InlineData("08:00:00")]
    [InlineData("eight")]
    public async Task CalculateAsync_StartTimeNotHHmm_Throws(string start)
    {
        await Assert.ThrowsAsync<PlanRequestException>(() =>
            _service.CalculateAsync(Request(new List<int> { _publishedId }, start)));
    }

    [Theory]
    [InlineData("26/10/2026")]
    [InlineData("2026-13-01")]
    public async Task CalculateAsync_PlanDateNotIsoDate_Throws(string date)
    {
        await Assert.ThrowsAsync<PlanRequestException>(() =>
            _service.CalculateAsync(Request(new List<int> { _publishedId }, null, date)));
    }
}
