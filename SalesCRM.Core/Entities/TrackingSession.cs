using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class TrackingSession : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public UserRole Role { get; set; }
    public DateTime SessionDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TrackingSessionStatus Status { get; set; } = TrackingSessionStatus.NotStarted;
    public decimal TotalDistanceKm { get; set; } = 0;
    public decimal AllowanceAmount { get; set; } = 0;
    public decimal AllowanceRatePerKm { get; set; } = 10.00m;

    public ICollection<LocationPing> LocationPings { get; set; } = new List<LocationPing>();
    public DailyAllowance? DailyAllowance { get; set; }
}
