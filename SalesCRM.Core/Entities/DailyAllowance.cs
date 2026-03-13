namespace SalesCRM.Core.Entities;

public class DailyAllowance : BaseEntity
{
    public int SessionId { get; set; }
    public TrackingSession? Session { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime AllowanceDate { get; set; }
    public decimal TotalDistanceKm { get; set; } = 0;
    public decimal RatePerKm { get; set; } = 10.00m;
    public decimal GrossAllowance { get; set; } = 0;
    public bool Approved { get; set; } = false;
    public int? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Remarks { get; set; }
}
