namespace SalesCRM.Core.DTOs.Demos;

public class DemoAssignmentDto
{
    public int Id { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public int AssignedToId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public int? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime ScheduledDate { get; set; }
    public string ScheduledStartTime { get; set; } = string.Empty;
    public string ScheduledEndTime { get; set; } = string.Empty;
    public string DemoMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? MeetingLink { get; set; }
    public string? Notes { get; set; }
    public string? Feedback { get; set; }
    public string? FeedbackSentiment { get; set; }
    public string? FeedbackAudioUrl { get; set; }
    public string? FeedbackVideoUrl { get; set; }
    public string? ScreenRecordingUrl { get; set; }
    public string? Outcome { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDemoRequest
{
    public int? LeadId { get; set; }
    public int SchoolId { get; set; }
    public int AssignedToId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public string ScheduledStartTime { get; set; } = "10:00";
    public string ScheduledEndTime { get; set; } = "11:00";
    public string DemoMode { get; set; } = "Offline";
    public string? MeetingLink { get; set; }
    public string? Notes { get; set; }
}

public class UpdateDemoRequest
{
    public string? Status { get; set; }
    public string? Feedback { get; set; }
    public string? FeedbackSentiment { get; set; }
    public string? FeedbackAudioUrl { get; set; }
    public string? FeedbackVideoUrl { get; set; }
    public string? ScreenRecordingUrl { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string? ScheduledStartTime { get; set; }
    public string? ScheduledEndTime { get; set; }
    public string? MeetingLink { get; set; }
    public int? AssignedToId { get; set; }
}
