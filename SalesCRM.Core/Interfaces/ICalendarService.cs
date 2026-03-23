using SalesCRM.Core.DTOs.Calendar;

namespace SalesCRM.Core.Interfaces;

public interface ICalendarService
{
    Task<List<CalendarEventDto>> GetEventsAsync(int userId, string from, string to);
    Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventRequest request, int userId);
    Task<CalendarEventDto?> UpdateEventAsync(int id, UpdateCalendarEventRequest request);
    Task<bool> DeleteEventAsync(int id);
}
