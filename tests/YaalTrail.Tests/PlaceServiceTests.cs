using Microsoft.Extensions.Caching.Memory;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// Visitor catalogue and place detail: FR-01 to FR-13, NFR-01, NFR-02.
public class PlaceServiceTests
{
    private readonly AppDbContext _db = TestData.NewDatabase();
    private readonly FakeClock _clock = FakeClock.MondayMorning(); // Monday 08:00 in Sri Lanka
    private readonly CatalogueCache _cache = new(new MemoryCache(new MemoryCacheOptions()));
    private readonly PlaceService _service;
    private readonly Category _religious = new() { Name = "Religious" };
    private readonly Category _heritage = new() { Name = "Heritage" };

    public PlaceServiceTests()
    {
        _service = new PlaceService(_db, _clock, _cache);
        _db.Categories.AddRange(_religious, _heritage);
        _db.SaveChanges();
    }

    private Place AddPlace(string name, Category category, decimal distanceKm, bool published = true,
        string? description = null, decimal entryFee = 0, List<OpeningHour>? hours = null)
    {
        var place = new Place
        {
            Name = name, Category = category, DistanceKm = distanceKm, IsPublished = published,
            Description = description, EntryFee = entryFee, Latitude = TestData.ReferenceLat, Longitude = TestData.ReferenceLon,
            OpeningHours = hours ?? new List<OpeningHour>(),
        };
        _db.Places.Add(place);
        _db.SaveChanges();
        return place;
    }

    private void AddCatalogue()
    {
        AddPlace("Nallur Kovil", _religious, 1.5m, description: "Hindu temple");
        AddPlace("Jaffna Fort", _heritage, 2.5m, description: "A Dutch fort by the lagoon");
        AddPlace("Archaeology Museum", _heritage, 0.8m);
        AddPlace("Hidden Draft", _religious, 0.1m, published: false);
    }

    // ---------- catalogue ----------

    [Fact]
    public async Task GetPlacesAsync_NoFilters_ReturnsOnlyPublishedPlacesNearestFirst()
    {
        AddCatalogue();

        PlaceListResponseDto result = await _service.GetPlacesAsync(null, null, null);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "Archaeology Museum", "Nallur Kovil", "Jaffna Fort" }, result.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task GetPlacesAsync_SortByName_ReturnsAlphabeticalOrder()
    {
        AddCatalogue();

        PlaceListResponseDto result = await _service.GetPlacesAsync(null, null, "NAME");

        Assert.Equal(new[] { "Archaeology Museum", "Jaffna Fort", "Nallur Kovil" }, result.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task GetPlacesAsync_CategoryFilter_ReturnsOnlyThatCategory()
    {
        AddCatalogue();

        PlaceListResponseDto result = await _service.GetPlacesAsync($"{_heritage.Id}", null, null);

        Assert.Equal(2, result.Count);
        Assert.All(result.Items, p => Assert.Equal("Heritage", p.Category));
    }

    [Fact]
    public async Task GetPlacesAsync_CategoryListWithJunk_IgnoresInvalidIds()
    {
        AddCatalogue();

        PlaceListResponseDto result = await _service.GetPlacesAsync($"abc, {_religious.Id},,{_heritage.Id}", null, null);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetPlacesAsync_SearchText_MatchesNameOrDescriptionIgnoringCase()
    {
        AddCatalogue();

        Assert.Equal("Jaffna Fort", Assert.Single((await _service.GetPlacesAsync(null, "DUTCH", null)).Items).Name);
        Assert.Equal("Nallur Kovil", Assert.Single((await _service.GetPlacesAsync(null, " nallur ", null)).Items).Name);
        Assert.Equal(0, (await _service.GetPlacesAsync(null, "beach", null)).Count);
    }

    [Theory]
    [InlineData(0, "Free entry")]
    [InlineData(1500, "LKR 1,500")]
    [InlineData(250.5, "LKR 250.5")]
    public async Task GetPlacesAsync_EntryFee_IsFormattedForVisitors(decimal fee, string expected)
    {
        AddPlace("Place", _religious, 1m, entryFee: fee);

        PlaceListResponseDto result = await _service.GetPlacesAsync(null, null, null);

        Assert.Equal(expected, Assert.Single(result.Items).EntryFeeText);
    }

    // NFR-02: the list is cached; Invalidate() (called by the admin services) shows changes at once.
    [Fact]
    public async Task GetPlacesAsync_AfterNewPlace_ServesCacheUntilInvalidated()
    {
        AddCatalogue();
        await _service.GetPlacesAsync(null, null, null);

        AddPlace("New Place", _religious, 3m);
        Assert.Equal(3, (await _service.GetPlacesAsync(null, null, null)).Count);

        _cache.Invalidate();
        Assert.Equal(4, (await _service.GetPlacesAsync(null, null, null)).Count);
    }

    // ---------- detail ----------

    [Fact]
    public async Task GetPlaceAsync_UnpublishedPlace_ReturnsNull()
    {
        Place draft = AddPlace("Hidden Draft", _religious, 1m, published: false);

        Assert.Null(await _service.GetPlaceAsync(draft.Id));
    }

    [Fact]
    public async Task GetPlaceAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await _service.GetPlaceAsync(999));
    }

    // FR-09: it is Monday 08:00 in Sri Lanka.
    [Theory]
    [InlineData(7, 17, true)]
    [InlineData(8, 17, true)]   // opens exactly now
    [InlineData(9, 17, false)]  // not open yet
    [InlineData(5, 8, false)]   // closes exactly now
    public async Task GetPlaceAsync_TodaysHours_IsOpenNowUsesSriLankaTime(int open, int close, bool expected)
    {
        Place place = AddPlace("Temple", _religious, 1m, hours: TestData.EveryDay(open, close));

        PlaceDetailDto detail = (await _service.GetPlaceAsync(place.Id))!;

        Assert.Equal(expected, detail.IsOpenNow);
        Assert.Equal($"{open:00}:00", detail.TodayOpenTime);
    }

    [Fact]
    public async Task GetPlaceAsync_ClosedToday_ShowsClosedAndNotOpen()
    {
        var hours = new List<OpeningHour> { new() { DayOfWeek = DayOfWeek.Monday, IsClosedAllDay = true } };
        Place place = AddPlace("Temple", _religious, 1m, hours: hours);

        PlaceDetailDto detail = (await _service.GetPlaceAsync(place.Id))!;

        Assert.False(detail.IsOpenNow);
        Assert.Equal(PlaceService.Closed, detail.TodayOpenTime);
    }

    // FR-12: anything not recorded says "Information not available".
    [Fact]
    public async Task GetPlaceAsync_MissingDescriptionTipsAndHours_ShowNotAvailable()
    {
        Place place = AddPlace("Temple", _religious, 1m);

        PlaceDetailDto detail = (await _service.GetPlaceAsync(place.Id))!;

        Assert.Equal(PlaceService.NotAvailable, detail.Description);
        Assert.Equal(PlaceService.NotAvailable, detail.TravelTips);
        Assert.Equal(PlaceService.NotAvailable, detail.TodayOpenTime);
        Assert.False(detail.IsOpenNow);
    }

    // FR-44: hours listed Monday to Sunday.
    [Fact]
    public async Task GetPlaceAsync_WeekOfHours_ListedMondayFirst()
    {
        Place place = AddPlace("Temple", _religious, 1m, hours: TestData.EveryDay(6, 18));

        PlaceDetailDto detail = (await _service.GetPlaceAsync(place.Id))!;

        Assert.Equal("Monday", detail.OpeningHours.First().Day);
        Assert.Equal("Sunday", detail.OpeningHours.Last().Day);
    }

    // FR-11: at most five photos, in display order.
    [Fact]
    public async Task GetPlaceAsync_SixPhotos_ReturnsFirstFiveInOrder()
    {
        Place place = AddPlace("Temple", _religious, 1m);
        for (int i = 6; i >= 1; i--)
            place.Images.Add(new PlaceImage { ImageUrl = $"https://example.com/{i}.jpg", SortOrder = i });
        _db.SaveChanges();

        PlaceDetailDto detail = (await _service.GetPlaceAsync(place.Id))!;

        Assert.Equal(5, detail.Images.Count);
        Assert.Equal("https://example.com/1.jpg", detail.Images[0].ImageUrl);
    }
}
