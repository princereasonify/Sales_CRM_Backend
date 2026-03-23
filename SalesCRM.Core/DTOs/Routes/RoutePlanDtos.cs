namespace SalesCRM.Core.DTOs.Routes;

public class RoutePlanDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime PlanDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Stops { get; set; } = "[]";
    public decimal? TotalEstimatedDistanceKm { get; set; }
    public int? TotalEstimatedDurationMinutes { get; set; }
    public decimal? TotalActualDistanceKm { get; set; }
    public string OptimizationMethod { get; set; } = "Manual";
    public DateTime CreatedAt { get; set; }
}

public class CreateRoutePlanRequest
{
    public DateTime PlanDate { get; set; }
    public string Stops { get; set; } = "[]"; // JSON array of {schoolId, schoolName, lat, lon, order}
    public decimal? TotalEstimatedDistanceKm { get; set; }
    public int? TotalEstimatedDurationMinutes { get; set; }
    public string OptimizationMethod { get; set; } = "Manual";
}

public class UpdateRoutePlanRequest
{
    public string? Stops { get; set; }
    public string? Status { get; set; }
    public decimal? TotalActualDistanceKm { get; set; }
}
