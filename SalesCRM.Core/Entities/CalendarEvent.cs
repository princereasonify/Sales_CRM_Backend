using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class CalendarEvent : BaseEntity
{
    public int UserId { get; set; }
    public CalendarEventType EventType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool AllDay { get; set; }
    public int? SchoolId { get; set; }
    public int? LeadId { get; set; }
    public int? DemoAssignmentId { get; set; }
    public int? OnboardAssignmentId { get; set; }
    public bool IsCompleted { get; set; }

    public User User { get; set; } = null!;
    public School? School { get; set; }
    public Lead? Lead { get; set; }
    public DemoAssignment? DemoAssignment { get; set; }
    public OnboardAssignment? OnboardAssignment { get; set; }
}
