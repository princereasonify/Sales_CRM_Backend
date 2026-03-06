using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class Activity : BaseEntity
{
    public ActivityType Type { get; set; }
    public DateTime Date { get; set; }
    public ActivityOutcome Outcome { get; set; }
    public string? Notes { get; set; }
    public bool GpsVerified { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public int FoId { get; set; }
    public User Fo { get; set; } = null!;

    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;
}
