using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class TargetAssignment : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal TargetAmount { get; set; }
    public decimal AchievedAmount { get; set; }

    public int NumberOfSchools { get; set; }
    public int AchievedSchools { get; set; }

    public int? NumberOfLogins { get; set; }
    public int? AchievedLogins { get; set; }

    public int? NumberOfStudents { get; set; }
    public int? AchievedStudents { get; set; }

    public PeriodType PeriodType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public TargetStatus Status { get; set; } = TargetStatus.Pending;

    public int AssignedToId { get; set; }
    public User AssignedTo { get; set; } = null!;

    public int AssignedById { get; set; }
    public User AssignedBy { get; set; } = null!;

    public int? ParentTargetId { get; set; }
    public TargetAssignment? ParentTarget { get; set; }
    public ICollection<TargetAssignment> SubTargets { get; set; } = new List<TargetAssignment>();

    // Review trail
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}
