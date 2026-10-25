using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// SRS 4.6 place management: FR-39 to FR-44, FR-47, FR-48, BR-01, BR-05,
// using the in-memory database.
public class AdminPlaceServiceTests
{
    private readonly AppDbContext _db = TestData.NewDatabase();
    private readonly FakeClock _clock = FakeClock.MondayMorning();
    private readonly SpyCatalogueCache _cache = new();
    private readonly AdminPlaceService _service;
    private readonly int _categoryId;
    private readonly int _adminId;

    public AdminPlaceServiceTests()
    {
        _service = new AdminPlaceService(_db, _cache, new DistanceService(TestData.Planner()), _clock, TestData.Planner());

        var category = new Category { Name = "Heritage" };
        var admin = new AdminUser { Email = "admin@yaaltrail.lk", DisplayName = "Test Admin", PasswordHash = "x" };
        _db.Categories.Add(category);
        _db.AdminUsers.Add(admin);
        _db.SaveChanges();
        _categoryId = category.Id;
        _adminId = admin.Id;
    }

    private static List<AdminOpeningHourDto> MondayHours(string? open, string? close, bool closed = false) =>
        new() { new AdminOpeningHourDto("Monday", open, close, closed) };

    // A valid request about 1 km north of Chundikuli.
    private AdminPlaceRequestDto Request(
        decimal latitude = TestData.ReferenceLat + 0.009m,
        decimal longitude = TestData.ReferenceLon,
        int? categoryId = null,
        List<AdminOpeningHourDto>? hours = null,
        decimal? distanceKm = 1.5m,
        bool isPublished = true,
        string name = "Jaffna Fort")
    {
        return new AdminPlaceRequestDto(name, categoryId ?? _categoryId, "A Dutch fort.", latitude, longitude,
            distanceKm, 0m, "Go early.", isPublished, hours ?? MondayHours("08:00", "18:00"));
    }

    private async Task<int> CreatePlace(bool isPublished = true)
    {
        AdminPlaceDto created = await _service.CreateAsync(Request(isPublished: isPublished), _adminId);
        return created.Id;
    }

    // ---------- FR-39, FR-48 create ----------

    [Fact]
    public async Task CreateAsync_ValidPlace_SavesItWithTimestampsAndAdmin()
    {
        AdminPlaceDto place = await _service.CreateAsync(Request(), _adminId);

        Assert.Equal("Jaffna Fort", place.Name);
        Assert.Equal("Heritage", place.Category);
        Assert.Equal(_clock.UtcNow, place.CreatedAt);
        Assert.Equal(_clock.UtcNow, place.UpdatedAt);
        Assert.Equal(_adminId, place.MaintainedByAdminId);
        Assert.Equal("Test Admin", place.MaintainedBy);
        Assert.Equal("08:00", Assert.Single(place.OpeningHours).OpenTime);
    }

    // The catalogue cache must be cleared so visitors see the change at once.
    [Fact]
    public async Task CreateAsync_ValidPlace_InvalidatesCatalogueCache()
    {
        await _service.CreateAsync(Request(), _adminId);

        Assert.Equal(1, _cache.InvalidateCount);
    }

    [Fact]
    public async Task CreateAsync_NoDistanceGiven_EstimatesRoadDistance()
    {
        // 0.009 degrees north is about 1.0 km straight line, so about 1.3 km by road.
        AdminPlaceDto place = await _service.CreateAsync(Request(distanceKm: null), _adminId);

        Assert.InRange(place.DistanceKm, 1.25m, 1.35m);
    }

    // ---------- FR-42, BR-01 radius ----------

    [Fact]
    public async Task CreateAsync_CoordinatesMoreThan25KmAway_ThrowsWithDistanceInReason()
    {
        // 0.3 degrees north of Chundikuli is about 33 km away.
        var error = await Assert.ThrowsAsync<AdminRequestException>(() =>
            _service.CreateAsync(Request(latitude: TestData.ReferenceLat + 0.3m), _adminId));

        string reason = Assert.Single(error.Errors);
        Assert.Contains("33.4 km", reason);
        Assert.Contains("25 km", reason);
        Assert.Empty(_db.Places);
        Assert.Equal(0, _cache.InvalidateCount);
    }

    [Fact]
    public async Task CreateAsync_CoordinatesJustInside25Km_IsAccepted()
    {
        // 0.2 degrees north is about 22.2 km.
        AdminPlaceDto place = await _service.CreateAsync(Request(latitude: TestData.ReferenceLat + 0.2m), _adminId);

        Assert.True(place.Id > 0);
    }

    // ---------- FR-43, FR-44 opening hours ----------

    [Theory]
    [InlineData("18:00", "09:00")] // closes before it opens
    [InlineData("09:00", "09:00")] // closes the moment it opens
    public async Task CreateAsync_ClosingTimeNotLaterThanOpening_ThrowsWithDayInReason(string open, string close)
    {
        var error = await Assert.ThrowsAsync<AdminRequestException>(() =>
            _service.CreateAsync(Request(hours: MondayHours(open, close)), _adminId));

        string reason = Assert.Single(error.Errors);
        Assert.StartsWith("Monday", reason);
        Assert.Contains("later than opening time", reason);
    }

    [Fact]
    public async Task CreateAsync_DayMarkedClosedWithoutTimes_IsAccepted()
    {
        AdminPlaceDto place = await _service.CreateAsync(Request(hours: MondayHours(null, null, closed: true)), _adminId);

        AdminOpeningHourDto monday = Assert.Single(place.OpeningHours);
        Assert.True(monday.IsClosed);
        Assert.Null(monday.OpenTime);
    }

    // FR-43: "unless the day is marked closed": wrong times are ignored on a closed day.
    [Fact]
    public async Task CreateAsync_DayMarkedClosedWithBackwardsTimes_IsAccepted()
    {
        AdminPlaceDto place = await _service.CreateAsync(Request(hours: MondayHours("18:00", "09:00", closed: true)), _adminId);

        Assert.True(Assert.Single(place.OpeningHours).IsClosed);
    }

    [Fact]
    public async Task CreateAsync_DifferentHoursForEachDay_AreAllSavedMondayFirst()
    {
        var hours = new List<AdminOpeningHourDto>
        {
            new("Sunday", null, null, true),
            new("Monday", "08:00", "12:00", false),
            new("saturday", "10:00", "16:00", false), // day names are not case-sensitive
        };

        AdminPlaceDto place = await _service.CreateAsync(Request(hours: hours), _adminId);

        Assert.Equal(new[] { "Monday", "Saturday", "Sunday" }, place.OpeningHours.Select(h => h.Day));
    }

    [Theory]
    [InlineData("08:00", null)]
    [InlineData(null, "18:00")]
    [InlineData("8 am", "6 pm")]
    public async Task CreateAsync_OpenDayWithMissingOrBadTimes_Throws(string? open, string? close)
    {
        await Assert.ThrowsAsync<AdminRequestException>(() =>
            _service.CreateAsync(Request(hours: MondayHours(open, close)), _adminId));
    }

    [Theory]
    [InlineData("Funday")]
    [InlineData("1")]
    public async Task CreateAsync_UnknownDayName_Throws(string day)
    {
        var hours = new List<AdminOpeningHourDto> { new(day, "08:00", "18:00", false) };

        var error = await Assert.ThrowsAsync<AdminRequestException>(() =>
            _service.CreateAsync(Request(hours: hours), _adminId));
        Assert.Contains("not a day of the week", Assert.Single(error.Errors));
    }

    [Fact]
    public async Task CreateAsync_SameDayTwice_Throws()
    {
        var hours = new List<AdminOpeningHourDto> { new("Monday", "08:00", "12:00", false), new("Monday", "13:00", "18:00", false) };

        var error = await Assert.ThrowsAsync<AdminRequestException>(() =>
            _service.CreateAsync(Request(hours: hours), _adminId));
        Assert.Contains("more than once", Assert.Single(error.Errors));
    }

    // ---------- BR-05 category ----------

    [Fact]
    public async Task CreateAsync_UnknownCategory_Throws()
    {
        var error = await Assert.ThrowsAsync<AdminRequestException>(() =>
            _service.CreateAsync(Request(categoryId: 999), _adminId));

        Assert.Contains("BR-05", Assert.Single(error.Errors));
    }

    // All problems are reported together so the admin can fix them in one go.
    [Fact]
    public async Task CreateAsync_SeveralProblems_ReportsAllOfThem()
    {
        var request = Request(latitude: TestData.ReferenceLat + 0.3m, categoryId: 999, hours: MondayHours("18:00", "09:00"),
            name: " ");

        var error = await Assert.ThrowsAsync<AdminRequestException>(() => _service.CreateAsync(request, _adminId));

        Assert.Equal(4, error.Errors.Count);
    }

    // ---------- FR-40 update ----------

    [Fact]
    public async Task UpdateAsync_ExistingPlace_ReplacesFieldsAndOpeningHours()
    {
        int id = await CreatePlace();
        _clock.Advance(TimeSpan.FromHours(2));

        var newHours = new List<AdminOpeningHourDto> { new("Tuesday", "09:00", "17:00", false) };
        AdminPlaceDto? updated = await _service.UpdateAsync(id, Request(name: "Jaffna Fort (Dutch)", hours: newHours), _adminId);

        Assert.NotNull(updated);
        Assert.Equal("Jaffna Fort (Dutch)", updated.Name);
        Assert.Equal("Tuesday", Assert.Single(updated.OpeningHours).Day);
        Assert.Equal(_clock.UtcNow, updated.UpdatedAt);
        Assert.Equal(_clock.UtcNow.AddHours(-2), updated.CreatedAt); // creation time is kept
        Assert.Single(_db.OpeningHours);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await _service.UpdateAsync(999, Request(), _adminId));
    }

    [Fact]
    public async Task UpdateAsync_InvalidRequest_ThrowsAndLeavesPlaceUnchanged()
    {
        int id = await CreatePlace();

        await Assert.ThrowsAsync<AdminRequestException>(() =>
            _service.UpdateAsync(id, Request(latitude: TestData.ReferenceLat + 0.3m), _adminId));

        Assert.Equal(TestData.ReferenceLat + 0.009m, (await _service.GetAsync(id))!.Latitude);
    }

    // ---------- reading ----------

    [Fact]
    public async Task GetAllAsync_PublishedAndUnpublishedPlaces_ReturnsBoth()
    {
        await _service.CreateAsync(Request(name: "B Published"), _adminId);
        await _service.CreateAsync(Request(name: "A Draft", isPublished: false), _adminId);

        List<AdminPlaceListItemDto> places = await _service.GetAllAsync();

        Assert.Equal(new[] { "A Draft", "B Published" }, places.Select(p => p.Name));
        Assert.Contains(places, p => !p.IsPublished);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await _service.GetAsync(999));
    }

    // ---------- FR-41 delete ----------

    [Fact]
    public async Task DeleteAsync_NotConfirmed_ThrowsWithSavedPlanCountAndKeepsPlace()
    {
        int id = await CreatePlace();
        AddSavedPlanUsing(id);
        AddSavedPlanUsing(id);

        var error = await Assert.ThrowsAsync<AdminRequestException>(() => _service.DeleteAsync(id, confirmed: false));

        Assert.Contains("2 saved plan(s)", error.Message);
        Assert.Equal(2, (await _service.GetAsync(id))!.SavedPlanCount);
    }

    [Fact]
    public async Task DeleteAsync_NotConfirmedAndNoSavedPlans_SaysSo()
    {
        int id = await CreatePlace();

        var error = await Assert.ThrowsAsync<AdminRequestException>(() => _service.DeleteAsync(id, confirmed: false));

        Assert.Contains("No saved plans", error.Message);
    }

    [Fact]
    public async Task DeleteAsync_Confirmed_RemovesPlaceAndInvalidatesCache()
    {
        int id = await CreatePlace();
        int before = _cache.InvalidateCount;

        bool deleted = await _service.DeleteAsync(id, confirmed: true);

        Assert.True(deleted);
        Assert.Null(await _service.GetAsync(id));
        Assert.Equal(before + 1, _cache.InvalidateCount);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAsync(999, confirmed: true));
    }

    private void AddSavedPlanUsing(int placeId)
    {
        var plan = new VisitPlan { ShareToken = Guid.NewGuid().ToString("N"), StartTime = new TimeOnly(8, 0), PlanDate = TestData.Monday };
        plan.Items.Add(new PlanItem { PlaceId = placeId, PlaceNameSnapshot = "Jaffna Fort", SequenceNo = 1 });
        _db.VisitPlans.Add(plan);
        _db.SaveChanges();
    }

    // ---------- FR-47 publish ----------

    [Fact]
    public async Task SetPublishedAsync_Unpublish_HidesPlaceAndRecordsAdmin()
    {
        int id = await CreatePlace();
        _clock.Advance(TimeSpan.FromMinutes(5));

        AdminPlaceDto? place = await _service.SetPublishedAsync(id, false, _adminId);

        Assert.False(place!.IsPublished);
        Assert.Equal(_clock.UtcNow, place.UpdatedAt);
    }

    [Fact]
    public async Task SetPublishedAsync_Publish_MakesPlaceVisible()
    {
        int id = await CreatePlace(isPublished: false);

        AdminPlaceDto? place = await _service.SetPublishedAsync(id, true, _adminId);

        Assert.True(place!.IsPublished);
    }

    // BR-01 is checked again on publish, e.g. after the reference location was changed.
    [Fact]
    public async Task SetPublishedAsync_PlaceOutsideRadius_ThrowsAndStaysUnpublished()
    {
        var far = new Place
        {
            Name = "Far away", CategoryId = _categoryId, IsPublished = false,
            Latitude = TestData.ReferenceLat + 0.3m, Longitude = TestData.ReferenceLon,
        };
        _db.Places.Add(far);
        _db.SaveChanges();

        await Assert.ThrowsAsync<AdminRequestException>(() => _service.SetPublishedAsync(far.Id, true, _adminId));

        Assert.False(far.IsPublished);
    }

    [Fact]
    public async Task SetPublishedAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await _service.SetPublishedAsync(999, true, _adminId));
    }
}
