using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using YaalTrail.Api.Configuration;
using YaalTrail.Api.Dtos;

namespace YaalTrail.Api.Controllers;

// NFR-22: thin controller. These values are configuration, not calculations.
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly PlannerOptions _planner;

    public SettingsController(IOptions<PlannerOptions> planner)
    {
        _planner = planner.Value;
    }

    // FR-15: the map centres on this reference location the first time it is shown.
    [HttpGet("planner")]
    public ActionResult<PlannerSettingsDto> GetPlanner()
    {
        return Ok(new PlannerSettingsDto(
            _planner.ReferenceLatitude,
            _planner.ReferenceLongitude,
            _planner.RadiusKm,
            _planner.AverageSpeedKmh));
    }
}
