using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.VisitReports;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/visit-reports")]
public class VisitReportsController : BaseApiController
{
    private readonly IVisitReportService _svc;
    public VisitReportsController(IVisitReportService svc) => _svc = svc;

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
}
