namespace SalesCRM.Core.DTOs.Allowance;

public class AllowanceConfigDto
{
    public int Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public int? ScopeId { get; set; }
    public string? ScopeName { get; set; }
    public string? VehicleType { get; set; }
    public decimal RatePerKm { get; set; }
    public decimal? MaxDailyAllowance { get; set; }
    public decimal? MinDistanceForAllowance { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? SetByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAllowanceConfigRequest
{
    public string Scope { get; set; } = "Global";
    public int? ScopeId { get; set; }
    public string? VehicleType { get; set; }
    public decimal RatePerKm { get; set; }
    public decimal? MaxDailyAllowance { get; set; }
    public decimal? MinDistanceForAllowance { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class ResolvedAllowanceDto
{
    public decimal RatePerKm { get; set; }
    public decimal? MaxDailyAllowance { get; set; }
    public decimal? MinDistanceForAllowance { get; set; }
    public string ResolvedFrom { get; set; } = string.Empty; // "User", "Zone", "Region", "Global"
}
