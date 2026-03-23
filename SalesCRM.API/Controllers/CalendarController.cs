using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Calendar;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class CalendarController : BaseApiController
{
    private readonly ICalendarService _svc;
    public CalendarController(ICalendarService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetEvents([FromQuery] string from, [FromQuery] string to)
    {
        var events = await _svc.GetEventsAsync(UserId, from, to);
        return Ok(ApiResponse<List<CalendarEventDto>>.Ok(events));
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateCalendarEventRequest request)
    {
        var ev = await _svc.CreateEventAsync(request, UserId);
        return Ok(ApiResponse<CalendarEventDto>.Ok(ev));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] UpdateCalendarEventRequest request)
    {
        var ev = await _svc.UpdateEventAsync(id, request);
        if (ev == null) return NotFound(ApiResponse<CalendarEventDto>.Fail("Event not found"));
        return Ok(ApiResponse<CalendarEventDto>.Ok(ev));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await _svc.DeleteEventAsync(id);
        if (!result) return NotFound(ApiResponse<bool>.Fail("Event not found"));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
