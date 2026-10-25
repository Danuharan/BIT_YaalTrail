using Microsoft.AspNetCore.Mvc;
using YaalTrail.Api.Data;

namespace YaalTrail.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(new { db = await _db.Database.CanConnectAsync() });
    }
}
