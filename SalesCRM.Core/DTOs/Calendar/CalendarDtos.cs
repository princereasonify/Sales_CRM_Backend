namespace SalesCRM.Core.DTOs.Calendar;

public class CalendarEventDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool AllDay { get; set; }
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public int? LeadId { get; set; }
    public int? DemoAssignmentId { get; set; }
    public int? OnboardAssignmentId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCalendarEventRequest
{
    public string EventType { get; set; } = "Other";
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool AllDay { get; set; }
    public int? SchoolId { get; set; }
    public int? LeadId { get; set; }
    public int? DemoAssignmentId { get; set; }
    public int? OnboardAssignmentId { get; set; }
}

public class UpdateCalendarEventRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool? IsCompleted { get; set; }
}
