using Microsoft.Extensions.Options;
using YaalTrail.Api.Configuration;

namespace YaalTrail.Api.Services;

public interface IDistanceService
{
    double StraightLineKm(double lat1, double lon1, double lat2, double lon2);
    double RoadKm(double lat1, double lon1, double lat2, double lon2);
}

// Pure calculation, no database access, so it can be unit tested (NFR-23).
public class DistanceService : IDistanceService
{
    private const double EarthRadiusKm = 6371.0;

    private readonly double _roadFactor;

    public DistanceService(IOptions<PlannerOptions> options)
    {
        _roadFactor = (double)options.Value.RoadFactor;
    }

    // FR-24: haversine formula = great-circle ("as the crow flies") distance.
    public double StraightLineKm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // FR-26, TBD-02: roads are not straight, so estimate road distance
    // as straight-line distance times a configured road factor (default 1.3).
    public double RoadKm(double lat1, double lon1, double lat2, double lon2)
    {
        return StraightLineKm(lat1, lon1, lat2, lon2) * _roadFactor;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
