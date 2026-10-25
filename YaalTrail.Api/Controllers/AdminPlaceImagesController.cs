using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Services;

namespace YaalTrail.Api.Controllers;

// FR-11, FR-45: a place's photos. BR-02, FR-52: every action needs a valid admin token.
// NFR-22: thin controller; the rules are in AdminPlaceImageService.
[ApiController]
[Route("api/admin/places/{id:int}/images")]
[Authorize(Roles = AdminAuthService.AdminRole)]
public class AdminPlaceImagesController : ControllerBase
{
    // A little above 3 MB, so the form fields and multipart framing fit;
    // the 3 MB limit on the file itself is checked in the service.
    private const long MaxRequestBytes = AdminPlaceImageService.MaxFileBytes + 64 * 1024;

    private readonly IAdminPlaceImageService _images;

    public AdminPlaceImagesController(IAdminPlaceImageService images)
    {
        _images = images;
    }

    // FR-48: the signed-in admin, taken from the token's "sub" claim.
    private int AdminId => int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    // POST api/admin/places/5/images (multipart/form-data: file, caption)
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<ActionResult<AdminPlaceImageDto>> Upload(int id, [FromForm] AdminPhotoUploadForm form)
    {
        try
        {
            await using Stream stream = form.File!.OpenReadStream();
            AdminPlaceImageDto? image = await _images.AddAsync(id, stream, form.File.Length, form.Caption, AdminId);
            return image == null ? NotFound() : StatusCode(StatusCodes.Status201Created, image);
        }
        catch (AdminRequestException ex)
        {
            return InvalidPhoto(ex.Message);
        }
        catch (PhotoStorageException ex)
        {
            return StorageFailed(ex.Message);
        }
    }

    // PUT api/admin/places/5/images/order   body: [12, 9, 15]
    [HttpPut("order")]
    public async Task<ActionResult<List<AdminPlaceImageDto>>> Reorder(int id, [FromBody] List<int> imageIds)
    {
        try
        {
            List<AdminPlaceImageDto>? images = await _images.ReorderAsync(id, imageIds, AdminId);
            return images == null ? NotFound() : Ok(images);
        }
        catch (AdminRequestException ex)
        {
            return InvalidPhoto(ex.Message);
        }
    }

    // DELETE api/admin/places/5/images/12
    [HttpDelete("{imageId:int}")]
    public async Task<IActionResult> Delete(int id, int imageId)
    {
        return await _images.DeleteAsync(id, imageId, AdminId) ? NoContent() : NotFound();
    }

    private BadRequestObjectResult InvalidPhoto(string message) =>
        BadRequest(new ProblemDetails { Title = "Invalid photo", Detail = message });

    // 502: the storage service, not the request, is the problem.
    private ObjectResult StorageFailed(string message) =>
        StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails { Title = "Photo storage error", Detail = message });
}
