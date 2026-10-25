using Microsoft.AspNetCore.Mvc;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Services;

namespace YaalTrail.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    // FR-02
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories()
    {
        return Ok(await _categories.GetAllAsync());
    }
}
