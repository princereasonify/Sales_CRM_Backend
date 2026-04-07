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

    /// <summary>
    /// Generate a report. FO can generate for self. Managers can generate for any FO under them.
    /// type: FoDaily, FoWeekly, FoMonthly, or management types
    /// foId: target FO (optional — defaults to self for FO role)
    /// date: for daily. dateFrom/dateTo: for weekly/monthly/management.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromQuery] string type,
        [FromQuery] int? foId,
        [FromQuery] string? date,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo)
    {
        if (!Enum.TryParse<AiReportType>(type, true, out var reportType))
            return BadRequest(ApiResponse<object>.Fail("Invalid report type. Use: FoDaily, FoWeekly, FoMonthly, ZhWeekly, RhWeekly, ShWeekly, ScaWeekly"));

        // Determine target user
        var targetUserId = foId ?? UserId;

        // FO can only generate for self
        if (UserRole == "FO" && targetUserId != UserId)
            return Forbid();

        // Management reports: generate for self (the manager)
        if (reportType == AiReportType.ZhWeekly || reportType == AiReportType.RhWeekly ||
            reportType == AiReportType.ShWeekly || reportType == AiReportType.ScaWeekly)
        {
            var start = DateTime.TryParse(dateFrom, out var s) ? s : DateTime.UtcNow.AddDays(-6);
            var end = DateTime.TryParse(dateTo, out var e) ? e : DateTime.UtcNow;
            var mgmtReport = await _svc.GenerateManagementReportAsync(UserId, reportType, start, end);
            return Ok(ApiResponse<AiReportDetailDto>.Ok(MapReport(mgmtReport)));
        }

        // FO reports
        if (reportType == AiReportType.FoDaily)
        {
            var targetDate = DateTime.TryParse(date, out var d) ? d : DateTime.UtcNow;
            var report = await _svc.GenerateFoDailyReportAsync(targetUserId, targetDate);
            return Ok(ApiResponse<AiReportDetailDto>.Ok(MapReport(report)));
        }

        if (reportType == AiReportType.FoWeekly || reportType == AiReportType.FoMonthly)
        {
            DateTime start, end;
            if (reportType == AiReportType.FoMonthly)
            {
                if (DateTime.TryParse(dateFrom, out var mStart))
                {
                    start = new DateTime(mStart.Year, mStart.Month, 1);
                    end = start.AddMonths(1).AddDays(-1);
                }
                else
                {
                    start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                    end = DateTime.UtcNow;
                }
            }
            else
            {
                end = DateTime.TryParse(dateTo, out var e) ? e : DateTime.UtcNow;
                start = DateTime.TryParse(dateFrom, out var s) ? s : end.AddDays(-6);
            }

            var report = await _svc.GenerateFoPeriodReportAsync(targetUserId, reportType, start, end);
            return Ok(ApiResponse<AiReportDetailDto>.Ok(MapReport(report)));
        }

        return BadRequest(ApiResponse<object>.Fail("Unsupported report type"));
    }

    // ─── Legacy endpoints (kept for backward compatibility) ───
    [HttpPost("generate-my-daily")]
    public async Task<IActionResult> GenerateMyDaily([FromQuery] string? date)
    {
        var targetDate = DateTime.TryParse(date, out var d) ? d : DateTime.UtcNow;
        var report = await _svc.GenerateFoDailyReportAsync(UserId, targetDate);
        return Ok(ApiResponse<AiReportDetailDto>.Ok(MapReport(report)));
    }

    [HttpPost("generate-daily")]
    public async Task<IActionResult> GenerateDaily([FromQuery] string? date)
    {
        if (UserRole != "SH" && UserRole != "SCA") return Forbid();
        var targetDate = DateTime.TryParse(date, out var d) ? d : DateTime.UtcNow;
        await _svc.GenerateAllFoDailyReportsAsync(targetDate);
        return Ok(ApiResponse<string>.Ok("FO daily reports generation started"));
    }

    [HttpPost("generate-management")]
    public async Task<IActionResult> GenerateManagement([FromQuery] string? dateFrom, [FromQuery] string? dateTo)
    {
        if (UserRole != "RH" && UserRole != "SH" && UserRole != "SCA") return Forbid();
        var start = DateTime.TryParse(dateFrom, out var s) ? s : DateTime.UtcNow.AddDays(-6);
        var end = DateTime.TryParse(dateTo, out var e) ? e : DateTime.UtcNow;
        await _svc.GenerateAllManagementReportsAsync(start, end);
        return Ok(ApiResponse<string>.Ok("Management reports generation started"));
    }

    private static AiReportDetailDto MapReport(Core.Entities.AiReport r) => new()
    {
        Id = r.Id, UserId = r.UserId, ReportType = r.ReportType.ToString(),
        ReportDate = r.ReportDate, PeriodStart = r.PeriodStart, PeriodEnd = r.PeriodEnd,
        Status = r.Status.ToString(), OverallScore = r.OverallScore,
        OverallRating = r.OverallRating, GeneratedAt = r.GeneratedAt
    };
}
