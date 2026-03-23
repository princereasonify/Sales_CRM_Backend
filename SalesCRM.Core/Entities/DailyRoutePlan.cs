using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class DailyRoutePlan : BaseEntity
{
    public int UserId { get; set; }
    public DateTime PlanDate { get; set; }
    public RoutePlanStatus Status { get; set; } = RoutePlanStatus.Draft;
    public string Stops { get; set; } = "[]";   // JSON array of stops
    public decimal? TotalEstimatedDistanceKm { get; set; }
    public int? TotalEstimatedDurationMinutes { get; set; }
    public decimal? TotalActualDistanceKm { get; set; }
    public string OptimizationMethod { get; set; } = "Manual";

    public User User { get; set; } = null!;
}
