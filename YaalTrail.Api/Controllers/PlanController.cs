using Microsoft.AspNetCore.Mvc;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Services;

namespace YaalTrail.Api.Controllers;

// NFR-22: thin controller; ordering, timing and validation are in the plan services.
// Stateless: no visitor account and nothing is stored (NFR-16, BR-08).
[ApiController]
[Route("api/plan")]
public class PlanController : ControllerBase
{
    private readonly IPlanService _plans;

    public PlanController(IPlanService plans)
    {
        _plans = plans;
    }

    // FR-23 to FR-32, NFR-10
    // POST api/plan/calculate
    [HttpPost("calculate")]
    public async Task<ActionResult<PlanResponseDto>> Calculate([FromBody] PlanRequestDto request)
    {
        try
        {
            return Ok(await _plans.CalculateAsync(request));
        }
        catch (PlanRequestException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid plan request", Detail = ex.Message });
        }
    }
}
