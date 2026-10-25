using Microsoft.AspNetCore.Mvc;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Services;

namespace YaalTrail.Api.Controllers;

// NFR-22: thin controller; all filtering, sorting and formatting is in PlaceService.
[ApiController]
[Route("api/places")]
public class PlacesController : ControllerBase
{
    private readonly IPlaceService _places;

    public PlacesController(IPlaceService places)
    {
        _places = places;
    }

    // FR-01 to FR-05, FR-07, FR-14
    // GET api/places?categories=1,2&search=text&sort=distance|name
    [HttpGet]
    public async Task<ActionResult<PlaceListResponseDto>> GetPlaces(
        [FromQuery] string? categories,
        [FromQuery] string? search,
        [FromQuery] string? sort)
    {
        return Ok(await _places.GetPlacesAsync(categories, search, sort));
    }

    // FR-08 to FR-13
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlaceDetailDto>> GetPlace(int id)
    {
        PlaceDetailDto? place = await _places.GetPlaceAsync(id);
        return place == null ? NotFound() : Ok(place);
    }
}
