namespace SalesCRM.Core.Entities;

public class SchoolVisitLog : BaseEntity
{
    public int SessionId { get; set; }
    public int UserId { get; set; }
    public int SchoolId { get; set; }
    public int? EnterEventId { get; set; }
    public int? ExitEventId { get; set; }
    public DateTime EnteredAt { get; set; }
    public DateTime? ExitedAt { get; set; }
    public decimal? DurationMinutes { get; set; }
    public DateTime VisitDate { get; set; }

    // Navigation
    public TrackingSession Session { get; set; } = null!;
    public User User { get; set; } = null!;
    public School School { get; set; } = null!;
    public GeofenceEvent? EnterEvent { get; set; }
    public GeofenceEvent? ExitEvent { get; set; }
}
