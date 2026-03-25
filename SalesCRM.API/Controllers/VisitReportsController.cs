using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.VisitReports;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/visit-reports")]
public class VisitReportsController : BaseApiController
{
    private readonly IVisitReportService _svc;
    private readonly IGcpStorageService _gcpStorage;
    public VisitReportsController(IVisitReportService svc, IGcpStorageService gcpStorage)
    {
        _svc = svc;
        _gcpStorage = gcpStorage;
    }

    [HttpPost]
    public async Task<IActionResult> CreateVisitReport([FromBody] CreateVisitReportRequest request)
    {
        var report = await _svc.CreateVisitReportAsync(request, UserId);
        return Ok(ApiResponse<VisitReportDto>.Ok(report));
    }

    [HttpGet]
    public async Task<IActionResult> GetVisitReports([FromQuery] int? userId, [FromQuery] string? date)
    {
        var reports = await _svc.GetVisitReportsByUserAsync(userId ?? UserId, date);
        return Ok(ApiResponse<List<VisitReportDto>>.Ok(reports));
    }

    [HttpGet("fields")]
    public async Task<IActionResult> GetVisitFieldConfigs()
    {
        var fields = await _svc.GetVisitFieldConfigsAsync();
        return Ok(ApiResponse<List<VisitFieldConfigDto>>.Ok(fields));
    }

    [HttpPost("fields")]
    public async Task<IActionResult> CreateVisitFieldConfig([FromBody] CreateVisitFieldConfigRequest request)
    {
        var field = await _svc.CreateVisitFieldConfigAsync(request, UserId);
        return Ok(ApiResponse<VisitFieldConfigDto>.Ok(field));
    }

    [HttpPut("fields/{id}")]
    public async Task<IActionResult> UpdateVisitFieldConfig(int id, [FromBody] CreateVisitFieldConfigRequest request)
    {
        var field = await _svc.UpdateVisitFieldConfigAsync(id, request);
        if (field == null) return NotFound(ApiResponse<VisitFieldConfigDto>.Fail("Not found"));
        return Ok(ApiResponse<VisitFieldConfigDto>.Ok(field));
    }

    [HttpDelete("fields/{id}")]
    public async Task<IActionResult> DeleteVisitFieldConfig(int id)
    {
        var result = await _svc.DeleteVisitFieldConfigAsync(id);
        if (!result) return NotFound(ApiResponse<bool>.Fail("Not found"));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ─── Media Upload (Photo/Video/Audio) ─────────────────────────────────

    [HttpPost("upload-media")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB for video
    public async Task<IActionResult> UploadMedia(IFormFile file, [FromForm] string mediaType, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));

        var allowedTypes = mediaType?.ToLower() switch
        {
            "photo" => new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" },
            "video" => new[] { "video/mp4", "video/quicktime", "video/webm", "video/3gpp" },
            "audio" => new[] { "audio/mpeg", "audio/wav", "audio/ogg", "audio/mp4", "audio/webm", "audio/aac" },
            _ => Array.Empty<string>()
        };

        if (allowedTypes.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Invalid media type. Use: photo, video, or audio."));

        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(ApiResponse<object>.Fail($"Invalid file type for {mediaType}. Allowed: {string.Join(", ", allowedTypes)}"));

        var maxSize = mediaType?.ToLower() == "video" ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
        if (file.Length > maxSize)
            return BadRequest(ApiResponse<object>.Fail($"File size must be under {maxSize / (1024 * 1024)}MB."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var folder = mediaType?.ToLower() switch { "video" => "VisitVideos", "audio" => "VisitAudio", _ => "VisitPhotos" };
        var objectName = $"{folder}/{Guid.NewGuid():N}{ext}";

        await using var stream = file.OpenReadStream();
        var result = await _gcpStorage.UploadFileAsync(objectName, stream, file.ContentType, cancellationToken);

        if (!result.Success)
            return StatusCode(500, ApiResponse<object>.Fail(result.Error ?? "Upload failed."));

        return Ok(ApiResponse<object>.Ok(new { url = result.PublicUrl, mediaType, contentType = file.ContentType }));
    }
}
