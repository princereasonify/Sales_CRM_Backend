using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class WeeklyPlan : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime WeekStartDate { get; set; }
    public DateTime WeekEndDate { get; set; }
    public string PlanData { get; set; } = "[]";
    public WeeklyPlanStatus Status { get; set; } = WeeklyPlanStatus.Draft;

    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ManagerEdits { get; set; }
}
