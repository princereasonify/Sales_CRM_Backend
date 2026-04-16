using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class AllowanceConfig : BaseEntity
{
    public AllowanceScope Scope { get; set; }
    public int? ScopeId { get; set; }           // RegionId, ZoneId, or UserId
    public VehicleType? VehicleType { get; set; }
    public decimal RatePerKm { get; set; }
    public decimal? MaxDailyAllowance { get; set; }
    public decimal? MinDistanceForAllowance { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int SetById { get; set; }

    public User SetBy { get; set; } = null!;
}
