namespace SalesCRM.Core.Entities;

public class LocationPing : BaseEntity
{
    public int SessionId { get; set; }
    public TrackingSession? Session { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? AccuracyMetres { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? AltitudeMetres { get; set; }
    public decimal DistanceFromPrevKm { get; set; } = 0;
    public decimal CumulativeDistanceKm { get; set; } = 0;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public bool IsValid { get; set; } = true;
    public string? InvalidReason { get; set; }
}
