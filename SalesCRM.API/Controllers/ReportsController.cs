using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Reports;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class ReportsController : BaseApiController
{
    private readonly IReportService _svc;
    public ReportsController(IReportService svc) => _svc = svc;

    [HttpGet("user-performance")]
    public async Task<IActionResult> GetUserPerformance([FromQuery] ReportFilters filters)
    {
        var data = await _svc.GetUserPerformanceAsync(filters);
        return Ok(ApiResponse<List<UserPerformanceDto>>.Ok(data));
    }

    [HttpGet("school-visits")]
    public async Task<IActionResult> GetSchoolVisitSummary([FromQuery] ReportFilters filters)
    {
        var data = await _svc.GetSchoolVisitSummaryAsync(filters);
        return Ok(ApiResponse<List<SchoolVisitSummaryDto>>.Ok(data));
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipelineReport([FromQuery] ReportFilters filters)
    {
        var data = await _svc.GetPipelineReportAsync(filters);
        return Ok(ApiResponse<List<PipelineReportDto>>.Ok(data));
    }
}
