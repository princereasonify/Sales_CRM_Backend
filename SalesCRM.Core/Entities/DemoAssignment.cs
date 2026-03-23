using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class DemoAssignment : BaseEntity
{
    public int LeadId { get; set; }
    public int SchoolId { get; set; }
    public int RequestedById { get; set; }
    public int AssignedToId { get; set; }
    public int? ApprovedById { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TimeSpan ScheduledStartTime { get; set; }
    public TimeSpan ScheduledEndTime { get; set; }
    public string DemoMode { get; set; } = "Offline"; // Online, Offline, Hybrid
    public DemoStatus Status { get; set; } = DemoStatus.Requested;
    public string? MeetingLink { get; set; }
    public string? Notes { get; set; }
    public string? Feedback { get; set; }
    public DemoOutcome? Outcome { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RecordingUrl { get; set; }
    public bool RecordingConsentGiven { get; set; }

    // Navigation
    public Lead Lead { get; set; } = null!;
    public School School { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
    public User AssignedTo { get; set; } = null!;
    public User? ApprovedBy { get; set; }
}
