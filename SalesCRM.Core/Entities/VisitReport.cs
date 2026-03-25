using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class VisitReport : BaseEntity
{
    public int? SchoolVisitLogId { get; set; }
    public int? ActivityId { get; set; }
    public int UserId { get; set; }
    public int? SchoolId { get; set; }
    public VisitPurpose Purpose { get; set; }
    public int? PersonMetId { get; set; }
    public string? Outcome { get; set; }
    public string? Remarks { get; set; }
    public NextActionType NextAction { get; set; }
    public DateTime? NextActionDate { get; set; }
    public string? NextActionNotes { get; set; }
    public string? CustomFields { get; set; }   // JSON
    public string? Photos { get; set; }          // JSON array of URLs
    public string? Videos { get; set; }          // JSON array of URLs
    public string? AudioNotes { get; set; }      // JSON array of URLs
    public FeedbackSentiment? FeedbackSentiment { get; set; }
    public string? FeedbackText { get; set; }
    public string? FeedbackPersonName { get; set; }
    public string? FeedbackPersonDesignation { get; set; }

    // Navigation
    public SchoolVisitLog? SchoolVisitLog { get; set; }
    public Activity? Activity { get; set; }
    public User User { get; set; } = null!;
    public School? School { get; set; }
    public Contact? PersonMet { get; set; }
}
