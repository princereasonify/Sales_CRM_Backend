namespace SalesCRM.Core.DTOs.Onboarding;

public class OnboardAssignmentDto
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public string? LeadName { get; set; }
    public int? DealId { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int AssignedToId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public int AssignedById { get; set; }
    public string AssignedByName { get; set; } = string.Empty;
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Modules { get; set; }
    public int CompletionPercentage { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateOnboardRequest
{
    public int LeadId { get; set; }
    public int? DealId { get; set; }
    public int SchoolId { get; set; }
    public int AssignedToId { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public string? Modules { get; set; }
    public string? Notes { get; set; }
}

public class UpdateOnboardRequest
{
    public string? Status { get; set; }
    public int? CompletionPercentage { get; set; }
    public string? Notes { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
}
