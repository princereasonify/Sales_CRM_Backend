namespace SalesCRM.Core.DTOs.Geofence;

public class GeofenceEventDto
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int UserId { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal DistanceFromSchoolMetres { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class SchoolVisitLogDto
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; }
    public DateTime? ExitedAt { get; set; }
    public decimal? DurationMinutes { get; set; }
    public bool IsVerified { get; set; }
}

public class TimeBreakdownDto
{
    public decimal TotalVisitMinutes { get; set; }
    public decimal TotalTravelMinutes { get; set; }
    public decimal TotalIdleMinutes { get; set; }
    public int SchoolsVisitedCount { get; set; }
    public List<SchoolVisitLogDto> Visits { get; set; } = new();
}
