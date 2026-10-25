using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Services;

namespace YaalTrail.Api.Controllers;

// SRS 4.6: place management. BR-02, FR-52: every action needs a valid admin token.
// NFR-22: thin controller; validation and saving are in AdminPlaceService.
[ApiController]
[Route("api/admin/places")]
[Authorize(Roles = AdminAuthService.AdminRole)]
public class AdminPlacesController : ControllerBase
{
    private readonly IAdminPlaceService _places;

    public AdminPlacesController(IAdminPlaceService places)
    {
        _places = places;
    }

    // FR-48: the signed-in admin, taken from the token's "sub" claim.
    private int AdminId => int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    // GET api/admin/places (published and unpublished)
    [HttpGet]
    public async Task<ActionResult<List<AdminPlaceListItemDto>>> GetAll()
    {
        return Ok(await _places.GetAllAsync());
    }

    // GET api/admin/places/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminPlaceDto>> Get(int id)
    {
        AdminPlaceDto? place = await _places.GetAsync(id);
        return place == null ? NotFound() : Ok(place);
    }

    // FR-39, FR-42 to FR-44
    // POST api/admin/places
    [HttpPost]
    public async Task<ActionResult<AdminPlaceDto>> Create([FromBody] AdminPlaceRequestDto request)
    {
        try
        {
            AdminPlaceDto place = await _places.CreateAsync(request, AdminId);
            return CreatedAtAction(nameof(Get), new { id = place.Id }, place);
        }
        catch (AdminRequestException ex)
        {
            return InvalidPlace(ex);
        }
    }

    // FR-40, FR-42 to FR-44
    // PUT api/admin/places/5
    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminPlaceDto>> Update(int id, [FromBody] AdminPlaceRequestDto request)
    {
        try
        {
            AdminPlaceDto? place = await _places.UpdateAsync(id, request, AdminId);
            return place == null ? NotFound() : Ok(place);
        }
        catch (AdminRequestException ex)
        {
            return InvalidPlace(ex);
        }
    }

    // FR-41: DELETE api/admin/places/5?confirm=true
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool confirm = false)
    {
        try
        {
            return await _places.DeleteAsync(id, confirm) ? NoContent() : NotFound();
        }
        catch (AdminRequestException ex)
        {
            return InvalidPlace(ex);
        }
    }

    // FR-47: PATCH api/admin/places/5/publish
    [HttpPatch("{id:int}/publish")]
    public Task<ActionResult<AdminPlaceDto>> Publish(int id) => SetPublished(id, true);

    // FR-47: PATCH api/admin/places/5/unpublish
    [HttpPatch("{id:int}/unpublish")]
    public Task<ActionResult<AdminPlaceDto>> Unpublish(int id) => SetPublished(id, false);

    private async Task<ActionResult<AdminPlaceDto>> SetPublished(int id, bool published)
    {
        try
        {
            AdminPlaceDto? place = await _places.SetPublishedAsync(id, published, AdminId);
            return place == null ? NotFound() : Ok(place);
        }
        catch (AdminRequestException ex)
        {
            return InvalidPlace(ex);
        }
    }

    // FR-42: the reason is returned so the admin page can show it.
    private BadRequestObjectResult InvalidPlace(AdminRequestException ex)
    {
        var problem = new ProblemDetails { Title = "Invalid place", Detail = ex.Message };
        problem.Extensions["errors"] = ex.Errors;
        return BadRequest(problem);
    }
}
