namespace SalesCRM.Core.DTOs.Target;

public class TargetAssignmentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal TargetAmount { get; set; }
    public decimal AchievedAmount { get; set; }
    public int NumberOfSchools { get; set; }
    public int AchievedSchools { get; set; }

    public string PeriodType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public int AssignedToId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public string AssignedToRole { get; set; } = string.Empty;
    public string? AssignedToZone { get; set; }
    public string? AssignedToRegion { get; set; }

    public int AssignedById { get; set; }
    public string AssignedByName { get; set; } = string.Empty;
    public string AssignedByRole { get; set; } = string.Empty;

    public int? ParentTargetId { get; set; }
    public decimal SubTargetTotal { get; set; }
    public int SubTargetSchoolsTotal { get; set; }
    public int SubTargetCount { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; }
}
