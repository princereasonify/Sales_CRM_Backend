using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.SchoolProfile;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/school-profiles")]
public class SchoolProfileController : BaseApiController
{
    private readonly ISchoolProfileService _svc;
    public SchoolProfileController(ISchoolProfileService svc) => _svc = svc;

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
    public async Task<IActionResult> Create([FromBody] CreateSchoolProfileRequest request)
    {
        if (UserRole != "SCA") return Forbid();
        var data = await _svc.CreateAsync(request, UserId);
        return Ok(ApiResponse<SchoolProfileDto>.Ok(data));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSchoolProfileRequest request)
    {
        if (UserRole != "SCA") return Forbid();
        var data = await _svc.UpdateAsync(id, request);
        if (data == null) return NotFound(ApiResponse<object>.Fail("Profile not found"));
        return Ok(ApiResponse<SchoolProfileDto>.Ok(data));
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

    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportCsv()
    {
        if (UserRole != "SCA") return Forbid();
        var csv = await _svc.ExportCsvAsync();
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "school_profiles.csv");
    }
}
