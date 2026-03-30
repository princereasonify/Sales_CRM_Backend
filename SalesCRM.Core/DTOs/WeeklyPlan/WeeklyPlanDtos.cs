namespace SalesCRM.Core.DTOs.WeeklyPlan;

public class WeeklyPlanDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public DateTime WeekStartDate { get; set; }
    public DateTime WeekEndDate { get; set; }
    public string PlanData { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ManagerEdits { get; set; }
    public string? ApprovedPlanData { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateWeeklyPlanRequest
{
    public DateTime WeekStartDate { get; set; }
    public string PlanData { get; set; } = "[]";
}

public class UpdateWeeklyPlanRequest
{
    public string PlanData { get; set; } = "[]";
}

public class ManagerEditRequest
{
    public string ManagerEdits { get; set; } = "[]";
    public string? ReviewNotes { get; set; }
}

public class RejectPlanRequest
{
    public string? ReviewNotes { get; set; }
}
