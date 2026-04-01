using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/ai-reports")]
public class AiReportsController : BaseApiController
{
    private readonly IAiReportService _svc;
    public AiReportsController(IAiReportService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetReports([FromQuery] AiReportFilterDto filters)
    {
        var data = await _svc.GetReportsAsync(UserId, UserRole, filters);
        return Ok(ApiResponse<List<AiReportListDto>>.Ok(data));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetReport(int id)
    {
        var data = await _svc.GetReportAsync(id, UserId, UserRole);
        if (data == null) return NotFound(ApiResponse<object>.Fail("Report not found or access denied"));
        return Ok(ApiResponse<AiReportDetailDto>.Ok(data));
    }

    [HttpPost("generate-daily")]
    public async Task<IActionResult> GenerateDaily([FromQuery] string? date)
    {
        if (UserRole != "SH" && UserRole != "SCA")
            return Forbid();

        var targetDate = DateTime.TryParse(date, out var d) ? d : DateTime.UtcNow;
        await _svc.GenerateAllFoDailyReportsAsync(targetDate);
        return Ok(ApiResponse<string>.Ok("FO daily reports generation started"));
    }

    [HttpPost("generate-my-daily")]
    public async Task<IActionResult> GenerateMyDaily([FromQuery] string? date)
    {
        if (UserRole != "FO")
            return Forbid();

        var targetDate = DateTime.TryParse(date, out var d) ? d : DateTime.UtcNow;
        var report = await _svc.GenerateFoDailyReportAsync(UserId, targetDate);
        return Ok(ApiResponse<AiReportDetailDto>.Ok(new AiReportDetailDto
        {
            Id = report.Id,
            UserId = report.UserId,
            ReportType = report.ReportType.ToString(),
            ReportDate = report.ReportDate,
            Status = report.Status.ToString(),
            OverallScore = report.OverallScore,
            OverallRating = report.OverallRating,
            GeneratedAt = report.GeneratedAt
        }));
    }

    [HttpPost("generate-management")]
    public async Task<IActionResult> GenerateManagement([FromQuery] string? dateFrom, [FromQuery] string? dateTo)
    {
        if (UserRole != "RH" && UserRole != "SH" && UserRole != "SCA")
            return Forbid();

        var start = DateTime.TryParse(dateFrom, out var s) ? s : DateTime.UtcNow.AddDays(-15);
        var end = DateTime.TryParse(dateTo, out var e) ? e : DateTime.UtcNow;
        await _svc.GenerateAllManagementReportsAsync(start, end);
        return Ok(ApiResponse<string>.Ok("Management reports generation started"));
    }
}
