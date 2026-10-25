using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Services;

namespace YaalTrail.Api.Controllers;

// FR-46: category management. BR-02, FR-52: every action needs a valid admin token.
[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = AdminAuthService.AdminRole)]
public class AdminCategoriesController : ControllerBase
{
    private readonly IAdminCategoryService _categories;

    public AdminCategoriesController(IAdminCategoryService categories)
    {
        _categories = categories;
    }

    // GET api/admin/categories
    [HttpGet]
    public async Task<ActionResult<List<AdminCategoryDto>>> GetAll()
    {
        return Ok(await _categories.GetAllAsync());
    }

    // GET api/admin/categories/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminCategoryDto>> Get(int id)
    {
        AdminCategoryDto? category = await _categories.GetAsync(id);
        return category == null ? NotFound() : Ok(category);
    }

    // POST api/admin/categories
    [HttpPost]
    public async Task<ActionResult<AdminCategoryDto>> Create([FromBody] AdminCategoryRequestDto request)
    {
        try
        {
            AdminCategoryDto category = await _categories.CreateAsync(request);
            return CreatedAtAction(nameof(Get), new { id = category.Id }, category);
        }
        catch (AdminRequestException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid category", Detail = ex.Message });
        }
    }

    // FR-46 rename: PUT api/admin/categories/5
    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminCategoryDto>> Update(int id, [FromBody] AdminCategoryRequestDto request)
    {
        try
        {
            AdminCategoryDto? category = await _categories.UpdateAsync(id, request);
            return category == null ? NotFound() : Ok(category);
        }
        catch (AdminRequestException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid category", Detail = ex.Message });
        }
    }

    // FR-46: DELETE api/admin/categories/5 (409 if places still use it)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await _categories.DeleteAsync(id) ? NoContent() : NotFound();
        }
        catch (AdminConflictException ex)
        {
            return Conflict(new ProblemDetails { Title = "Category in use", Detail = ex.Message });
        }
    }
}
