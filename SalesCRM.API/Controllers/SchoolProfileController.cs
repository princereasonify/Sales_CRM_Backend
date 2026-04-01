using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.SchoolProfile;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/school-profiles")]
public class SchoolProfileController : BaseApiController
{
    private readonly ISchoolProfileService _svc;
    private readonly IGcpStorageService _gcpStorage;

    public SchoolProfileController(ISchoolProfileService svc, IGcpStorageService gcpStorage)
    {
        _svc = svc;
        _gcpStorage = gcpStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (UserRole != "SCA") return Forbid();
        var data = await _svc.GetAllAsync();
        return Ok(ApiResponse<List<SchoolProfileDto>>.Ok(data));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (UserRole != "SCA") return Forbid();
        var data = await _svc.GetByIdAsync(id);
        if (data == null) return NotFound(ApiResponse<object>.Fail("Profile not found"));
        return Ok(ApiResponse<SchoolProfileDto>.Ok(data));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSchoolProfileRequest request, [FromQuery] string format = "csv")
    {
        if (UserRole != "SCA") return Forbid();
        try
        {
            var data = await _svc.CreateAsync(request, UserId, format);
            return Ok(ApiResponse<SchoolProfileDto>.Ok(data, "Profile saved & email sent successfully"));
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException || ex.InnerException is System.Net.Mail.SmtpException)
        {
            return StatusCode(500, ApiResponse<object>.Fail("Profile saved but email failed: " + ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSchoolProfileRequest request, [FromQuery] string format = "csv")
    {
        if (UserRole != "SCA") return Forbid();
        try
        {
            var data = await _svc.UpdateAsync(id, request, format);
            if (data == null) return NotFound(ApiResponse<object>.Fail("Profile not found"));
            return Ok(ApiResponse<SchoolProfileDto>.Ok(data, "Profile saved & email sent successfully"));
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException || ex.InnerException is System.Net.Mail.SmtpException)
        {
            return StatusCode(500, ApiResponse<object>.Fail("Profile saved but email failed: " + ex.Message));
        }
    }

    [HttpGet("onboarded-schools")]
    public async Task<IActionResult> GetOnboardedSchools()
    {
        if (UserRole != "SCA") return Forbid();
        var data = await _svc.GetOnboardedSchoolsAsync();
        return Ok(ApiResponse<List<OnboardedSchoolDto>>.Ok(data));
    }

    [HttpGet("prefill/{schoolId}")]
    public async Task<IActionResult> GetPrefill(int schoolId)
    {
        if (UserRole != "SCA") return Forbid();
        var data = await _svc.GetPrefillAsync(schoolId);
        return Ok(ApiResponse<SchoolProfilePrefillDto>.Ok(data));
    }

    [HttpPost("upload-logo")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken cancellationToken)
    {
        if (UserRole != "SCA") return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/jpg" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(ApiResponse<object>.Fail("Only JPEG, PNG, and WebP images are allowed."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var objectName = $"SchoolLogos/{Guid.NewGuid():N}{ext}";

        await using var stream = file.OpenReadStream();
        var result = await _gcpStorage.UploadFileAsync(objectName, stream, file.ContentType, cancellationToken);

        if (!result.Success)
            return StatusCode(500, ApiResponse<object>.Fail(result.Error ?? "Upload failed."));

        return Ok(ApiResponse<object>.Ok(new { logoUrl = result.PublicUrl }, "Logo uploaded successfully."));
    }

    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportCsv()
    {
        if (UserRole != "SCA") return Forbid();
        var csv = await _svc.ExportCsvAsync();
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "school_profiles.csv");
    }
}
