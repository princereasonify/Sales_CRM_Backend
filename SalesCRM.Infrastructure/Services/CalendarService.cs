using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Calendar;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class CalendarService : ICalendarService
{
    private readonly IUnitOfWork _uow;
    public CalendarService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<CalendarEventDto>> GetEventsAsync(int userId, string from, string to)
    {
        if (!DateTime.TryParse(from, out var fd) || !DateTime.TryParse(to, out var td)) return new();
        var fromUtc = DateTime.SpecifyKind(fd.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(td.Date.AddDays(1), DateTimeKind.Utc);

        return await _uow.CalendarEvents.Query()
            .Include(c => c.School)
            .Where(c => c.UserId == userId && c.StartTime >= fromUtc && c.StartTime < toUtc)
            .OrderBy(c => c.StartTime)
            .Select(c => new CalendarEventDto
            {
                Id = c.Id, UserId = c.UserId, EventType = c.EventType.ToString(),
                Title = c.Title, Description = c.Description,
                StartTime = c.StartTime, EndTime = c.EndTime, AllDay = c.AllDay,
                SchoolId = c.SchoolId, SchoolName = c.School != null ? c.School.Name : null,
                LeadId = c.LeadId, DemoAssignmentId = c.DemoAssignmentId,
                OnboardAssignmentId = c.OnboardAssignmentId,
                IsCompleted = c.IsCompleted, CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventRequest request, int userId)
    {
        Enum.TryParse<CalendarEventType>(request.EventType, true, out var eventType);
        var ev = new CalendarEvent
        {
            UserId = userId, EventType = eventType, Title = request.Title,
            Description = request.Description,
            StartTime = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc),
            EndTime = DateTime.SpecifyKind(request.EndTime, DateTimeKind.Utc),
            AllDay = request.AllDay, SchoolId = request.SchoolId, LeadId = request.LeadId,
            DemoAssignmentId = request.DemoAssignmentId, OnboardAssignmentId = request.OnboardAssignmentId
        };
        await _uow.CalendarEvents.AddAsync(ev);
        await _uow.SaveChangesAsync();
        return new CalendarEventDto
        {
            Id = ev.Id, UserId = ev.UserId, EventType = ev.EventType.ToString(),
            Title = ev.Title, Description = ev.Description,
            StartTime = ev.StartTime, EndTime = ev.EndTime, AllDay = ev.AllDay,
            SchoolId = ev.SchoolId, IsCompleted = ev.IsCompleted, CreatedAt = ev.CreatedAt
        };
    }

    public async Task<CalendarEventDto?> UpdateEventAsync(int id, UpdateCalendarEventRequest request)
    {
        var ev = await _uow.CalendarEvents.GetByIdAsync(id);
        if (ev == null) return null;
        if (request.Title != null) ev.Title = request.Title;
        if (request.Description != null) ev.Description = request.Description;
        if (request.StartTime.HasValue) ev.StartTime = DateTime.SpecifyKind(request.StartTime.Value, DateTimeKind.Utc);
        if (request.EndTime.HasValue) ev.EndTime = DateTime.SpecifyKind(request.EndTime.Value, DateTimeKind.Utc);
        if (request.IsCompleted.HasValue) ev.IsCompleted = request.IsCompleted.Value;
        await _uow.SaveChangesAsync();
        return new CalendarEventDto
        {
            Id = ev.Id, UserId = ev.UserId, EventType = ev.EventType.ToString(),
            Title = ev.Title, Description = ev.Description,
            StartTime = ev.StartTime, EndTime = ev.EndTime, AllDay = ev.AllDay,
            SchoolId = ev.SchoolId, IsCompleted = ev.IsCompleted, CreatedAt = ev.CreatedAt
        };
    }

    public async Task<bool> DeleteEventAsync(int id)
    {
        var ev = await _uow.CalendarEvents.GetByIdAsync(id);
        if (ev == null) return false;
        await _uow.CalendarEvents.DeleteAsync(ev);
        await _uow.SaveChangesAsync();
        return true;
    }
}
