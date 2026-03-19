using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

public class ActivitiesController : BaseApiController
{
    private readonly IActivityService _activityService;
    private readonly IGcpStorageService _gcpStorage;

    public ActivitiesController(IActivityService activityService, IGcpStorageService gcpStorage)
    {
        _activityService = activityService;
        _gcpStorage = gcpStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetActivities(
        [FromQuery] PaginationParams pagination,
        [FromQuery] string? type)
    {
        var result = await _activityService.GetActivitiesAsync(UserId, pagination, type);
        return Ok(ApiResponse<PaginatedResult<ActivityDto>>.Ok(result));
    }

    [HttpGet("team/{foId}")]
    public async Task<IActionResult> GetTeamActivities(int foId)
    {
        var result = await _activityService.GetTeamActivitiesAsync(UserId, UserRole, foId);
        return Ok(ApiResponse<List<ActivityDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> CreateActivity([FromBody] CreateActivityRequest request)
    {
        var activity = await _activityService.CreateActivityAsync(request, UserId);
        return CreatedAtAction(nameof(GetActivities), ApiResponse<ActivityDto>.Ok(activity));
    }

    [HttpPost("upload-photo")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(IFormFile file, [FromForm] int activityId, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(ApiResponse<object>.Fail("Only JPEG, PNG, and WebP images are allowed."));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse<object>.Fail("File size must be under 5MB."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var objectName = $"SalesVisits/{Guid.NewGuid():N}{ext}";

        await using var stream = file.OpenReadStream();
        var result = await _gcpStorage.UploadFileAsync(objectName, stream, file.ContentType, cancellationToken);

        if (!result.Success)
            return StatusCode(500, ApiResponse<object>.Fail(result.Error ?? "Upload to cloud storage failed."));

        await _activityService.UpdatePhotoUrlAsync(activityId, UserId, result.PublicUrl!);

        return Ok(ApiResponse<object>.Ok(new { photoUrl = result.PublicUrl }, "Photo uploaded successfully."));
    }
}
