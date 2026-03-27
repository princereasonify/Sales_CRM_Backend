using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Demos;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class DemosController : BaseApiController
{
    private readonly IDemoService _svc;
    private readonly IGcpStorageService _storage;
    public DemosController(IDemoService svc, IGcpStorageService storage) { _svc = svc; _storage = storage; }

    [HttpGet]
    public async Task<IActionResult> GetDemos([FromQuery] string? status, [FromQuery] int? assignedToId,
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var (demos, total) = await _svc.GetDemosAsync(status, assignedToId, from, to, page, limit);
        return Ok(ApiResponse<object>.Ok(new { demos, total, page, limit }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDemo(int id)
    {
        var demo = await _svc.GetDemoByIdAsync(id);
        if (demo == null) return NotFound(ApiResponse<DemoAssignmentDto>.Fail("Demo not found"));
        return Ok(ApiResponse<DemoAssignmentDto>.Ok(demo));
    }

    [HttpPost]
    public async Task<IActionResult> CreateDemo([FromBody] CreateDemoRequest request)
    {
        var demo = await _svc.CreateDemoAsync(request, UserId);
        return Ok(ApiResponse<DemoAssignmentDto>.Ok(demo));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDemo(int id, [FromBody] UpdateDemoRequest request)
    {
        var demo = await _svc.UpdateDemoAsync(id, request);
        if (demo == null) return NotFound(ApiResponse<DemoAssignmentDto>.Fail("Demo not found"));
        return Ok(ApiResponse<DemoAssignmentDto>.Ok(demo));
    }

    [HttpPost("upload-feedback-media")]
    [RequestSizeLimit(52_428_800)] // 50MB
    public async Task<IActionResult> UploadFeedbackMedia(IFormFile file, [FromForm] string mediaType)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file provided"));

        var allowedTypes = mediaType switch
        {
            "video" => new[] { "video/mp4", "video/quicktime", "video/webm", "video/3gpp" },
            "audio" => new[] { "audio/mpeg", "audio/wav", "audio/ogg", "audio/mp4", "audio/webm", "audio/aac" },
            "screen" => new[] { "video/mp4", "video/webm", "video/quicktime" },
            _ => Array.Empty<string>()
        };

        if (allowedTypes.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Invalid mediaType. Use: video, audio, screen"));

        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(ApiResponse<object>.Fail($"Invalid file type: {file.ContentType}"));

        long maxSize = mediaType == "audio" ? 10_485_760 : 52_428_800; // audio 10MB, video/screen 50MB
        if (file.Length > maxSize)
            return BadRequest(ApiResponse<object>.Fail($"File too large. Max: {maxSize / 1048576}MB"));

        var ext = Path.GetExtension(file.FileName);
        var folder = mediaType switch
        {
            "video" => "SalesDemoFeedbacks/videos",
            "audio" => "SalesDemoFeedbacks/audio",
            "screen" => "SalesDemoFeedbacks/screen-recordings",
            _ => "SalesDemoFeedbacks"
        };
        var objectName = $"{folder}/{Guid.NewGuid()}{ext}";

        using var stream = file.OpenReadStream();
        var result = await _storage.UploadFileAsync(objectName, stream, file.ContentType);

        if (!result.Success)
            return StatusCode(500, ApiResponse<object>.Fail($"Upload failed: {result.Error}"));

        return Ok(ApiResponse<object>.Ok(new { url = result.PublicUrl, mediaType, contentType = file.ContentType }));
    }

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] string from, [FromQuery] string to)
    {
        var events = await _svc.GetDemoCalendarAsync(from, to, UserId);
        return Ok(ApiResponse<List<DemoAssignmentDto>>.Ok(events));
    }
}
