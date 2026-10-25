using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// FR-24, FR-26, TBD-02
public class DistanceServiceTests
{
    // Nallur Kandaswamy Kovil, as seeded in DbSeeder.
    private const double NallurLat = 9.674847;
    private const double NallurLon = 80.029322;

    private const double ChundikuliLat = (double)TestData.ReferenceLat;
    private const double ChundikuliLon = (double)TestData.ReferenceLon;

    private readonly DistanceService _service = new(TestData.Planner());

    [Fact]
    public void StraightLineKm_ChundikuliToNallur_IsAbout2Point2Km()
    {
        double km = _service.StraightLineKm(ChundikuliLat, ChundikuliLon, NallurLat, NallurLon);

        Assert.InRange(km, 2.15, 2.30);
    }

    [Fact]
    public void StraightLineKm_SamePoint_IsZero()
    {
        double km = _service.StraightLineKm(ChundikuliLat, ChundikuliLon, ChundikuliLat, ChundikuliLon);

        Assert.Equal(0, km, precision: 9);
    }

    // One degree of latitude is about 111.2 km on the haversine sphere (R = 6371 km).
    [Fact]
    public void StraightLineKm_OneDegreeOfLatitude_IsAbout111Km()
    {
        double km = _service.StraightLineKm(9.0, 80.0, 10.0, 80.0);

        Assert.Equal(111.19, km, precision: 1);
    }

    [Fact]
    public void StraightLineKm_ReversedDirection_GivesSameDistance()
    {
        double there = _service.StraightLineKm(ChundikuliLat, ChundikuliLon, NallurLat, NallurLon);
        double back = _service.StraightLineKm(NallurLat, NallurLon, ChundikuliLat, ChundikuliLon);

        Assert.Equal(there, back, precision: 9);
    }

    // TBD-02: road distance = straight-line distance * 1.3.
    [Fact]
    public void RoadKm_DefaultRoadFactor_IsStraightLineTimes1Point3()
    {
        double straight = _service.StraightLineKm(ChundikuliLat, ChundikuliLon, NallurLat, NallurLon);
        double road = _service.RoadKm(ChundikuliLat, ChundikuliLon, NallurLat, NallurLon);

        Assert.Equal(straight * 1.3, road, precision: 9);
    }

    [Fact]
    public void RoadKm_ConfiguredRoadFactor_IsUsed()
    {
        var service = new DistanceService(TestData.Planner(o => o.RoadFactor = 2m));

        double straight = service.StraightLineKm(ChundikuliLat, ChundikuliLon, NallurLat, NallurLon);
        double road = service.RoadKm(ChundikuliLat, ChundikuliLon, NallurLat, NallurLon);

        Assert.Equal(straight * 2, road, precision: 9);
    }

    [Fact]
    public void RoadKm_SamePoint_IsZero()
    {
        double km = _service.RoadKm(NallurLat, NallurLon, NallurLat, NallurLon);

        Assert.Equal(0, km, precision: 9);
    }
}
