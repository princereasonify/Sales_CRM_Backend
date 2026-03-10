using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

public class ActivitiesController : BaseApiController
{
    private readonly IActivityService _activityService;
    private readonly IWebHostEnvironment _env;

    public ActivitiesController(IActivityService activityService, IWebHostEnvironment env)
    {
        _activityService = activityService;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetActivities(
        [FromQuery] PaginationParams pagination,
        [FromQuery] string? type)
    {
        var result = await _activityService.GetActivitiesAsync(UserId, pagination, type);
        return Ok(ApiResponse<PaginatedResult<ActivityDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> CreateActivity([FromBody] CreateActivityRequest request)
    {
        var activity = await _activityService.CreateActivityAsync(request, UserId);
        return CreatedAtAction(nameof(GetActivities), ApiResponse<ActivityDto>.Ok(activity));
    }

    [HttpPost("upload-photo")]
    public async Task<IActionResult> UploadPhoto(IFormFile file, [FromForm] int activityId)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(ApiResponse<object>.Fail("Only JPEG, PNG, and WebP images are allowed."));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse<object>.Fail("File size must be under 5MB."));

        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads", "visit-photos");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var photoUrl = $"/uploads/visit-photos/{fileName}";

        await _activityService.UpdatePhotoUrlAsync(activityId, UserId, photoUrl);

        return Ok(ApiResponse<object>.Ok(new { photoUrl }, "Photo uploaded successfully."));
    }
}
