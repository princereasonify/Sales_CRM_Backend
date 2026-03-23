using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class OnboardAssignment : BaseEntity
{
    public int LeadId { get; set; }
    public int? DealId { get; set; }
    public int SchoolId { get; set; }
    public int AssignedToId { get; set; }
    public int AssignedById { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public OnboardStatus Status { get; set; } = OnboardStatus.Assigned;
    public string? Modules { get; set; }        // JSON array
    public string? TrainingDates { get; set; }   // JSON array
    public int CompletionPercentage { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Lead Lead { get; set; } = null!;
    public Deal? Deal { get; set; }
    public School School { get; set; } = null!;
    public User AssignedTo { get; set; } = null!;
    public User AssignedBy { get; set; } = null!;
}
