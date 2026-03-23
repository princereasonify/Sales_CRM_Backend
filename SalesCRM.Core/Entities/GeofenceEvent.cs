using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class GeofenceEvent : BaseEntity
{
    public int SessionId { get; set; }
    public int UserId { get; set; }
    public int SchoolId { get; set; }
    public GeofenceEventType EventType { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal DistanceFromSchoolMetres { get; set; }
    public DateTime RecordedAt { get; set; }

    // Navigation
    public TrackingSession Session { get; set; } = null!;
    public User User { get; set; } = null!;
    public School School { get; set; } = null!;
}
