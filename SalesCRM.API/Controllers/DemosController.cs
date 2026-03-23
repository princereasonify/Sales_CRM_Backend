using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Demos;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class DemosController : BaseApiController
{
    private readonly IDemoService _svc;
    public DemosController(IDemoService svc) => _svc = svc;

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

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] string from, [FromQuery] string to)
    {
        var events = await _svc.GetDemoCalendarAsync(from, to, UserId);
        return Ok(ApiResponse<List<DemoAssignmentDto>>.Ok(events));
    }
}
