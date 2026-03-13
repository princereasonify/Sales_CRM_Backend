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

    // ─── New fields for high-precision tracking ───
    public string? Provider { get; set; }         // GPS, Network, Fused
    public bool IsMocked { get; set; } = false;   // Mock location flag
    public decimal? BatteryLevel { get; set; }     // 0.0 - 1.0
    public bool IsFiltered { get; set; } = false;  // Filtered out by noise filter
    public string? FilterReason { get; set; }      // Why it was filtered
    public int? ClusterGroup { get; set; }         // Cluster assignment after processing
}
