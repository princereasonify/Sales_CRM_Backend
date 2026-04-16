namespace SalesCRM.Core.DTOs.Tracking;

// ─── Session DTOs ────────────────────────────────────────────────────────────

public class TrackingSessionDto
{
    public int SessionId { get; set; }
    public string Status { get; set; } = "not_started";
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string SessionDate { get; set; } = string.Empty;
    public decimal TotalDistanceKm { get; set; }
    public decimal AllowanceAmount { get; set; }
    public string? VehicleType { get; set; }
    public int PingCount { get; set; }
    // New fields
    public decimal RawDistanceKm { get; set; }
    public decimal FilteredDistanceKm { get; set; }
    public decimal ReconstructedDistanceKm { get; set; }
    public int FraudScore { get; set; }
    public bool IsSuspicious { get; set; }
    public List<string>? FraudFlags { get; set; }
}

public class ButtonStateDto
{
    public bool StartDayEnabled { get; set; }
    public bool EndDayEnabled { get; set; }
}

public class SessionResponseDto
{
    public bool Success { get; set; } = true;
    public TrackingSessionDto? Session { get; set; }
    public ButtonStateDto ButtonState { get; set; } = new();
}

public class StartDayRequest
{
    public string? VehicleType { get; set; }
}

// ─── Ping DTOs ───────────────────────────────────────────────────────────────

public class PingRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? AccuracyMetres { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? AltitudeMetres { get; set; }
    public DateTime? RecordedAt { get; set; }
    // New fields
    public string? Provider { get; set; }
    public bool IsMocked { get; set; } = false;
    public decimal? BatteryLevel { get; set; }
}

public class PingResponseDto
{
    public bool Success { get; set; } = true;
    public int PingId { get; set; }
    public bool IsValid { get; set; }
    public bool IsFiltered { get; set; }
    public string? FilterReason { get; set; }
    public decimal CumulativeDistanceKm { get; set; }
    public decimal AllowanceAmount { get; set; }
    public int FraudScore { get; set; }
}

// ─── Live Location DTOs ──────────────────────────────────────────────────────

public class LiveLocationDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public int? RegionId { get; set; }
    public string? RegionName { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? SpeedKmh { get; set; }
    public DateTime LastSeen { get; set; }
    public decimal TotalDistanceKm { get; set; }
    public decimal AllowanceAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    // New fields
    public int FraudScore { get; set; }
    public bool IsSuspicious { get; set; }
    public decimal? BatteryLevel { get; set; }
    // Heading and geofence
    public decimal? Heading { get; set; }          // Direction angle in degrees (0-360)
    public int? CurrentSchoolId { get; set; }       // Which school geofence they're currently in
    public string? CurrentSchoolName { get; set; }
    // Last known location (fallback when no today session)
    public bool IsLastKnownLocation { get; set; }  // true = location is from a past session, not today
    public string? LastSessionDate { get; set; }    // date of the session from which location was retrieved
}

// ─── Route DTOs ──────────────────────────────────────────────────────────────

public class RoutePointDto
{
    public decimal Lat { get; set; }
    public decimal Lon { get; set; }
    public DateTime RecordedAt { get; set; }
    public decimal? SpeedKmh { get; set; }
    public bool IsFiltered { get; set; }
    public int? ClusterGroup { get; set; }
}

public class RouteUserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class RouteResponseDto
{
    public bool Success { get; set; } = true;
    public RouteUserDto User { get; set; } = new();
    public TrackingSessionDto? Session { get; set; }
    public List<RoutePointDto> Route { get; set; } = new();
    public List<RoutePointDto> ReconstructedRoute { get; set; } = new();
}

// ─── Allowance DTOs ──────────────────────────────────────────────────────────

public class AllowanceDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AllowanceDate { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal RatePerKm { get; set; }
    public decimal GrossAmount { get; set; }
    public bool Approved { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Remarks { get; set; }
    // New fields
    public decimal RawDistanceKm { get; set; }
    public decimal FilteredDistanceKm { get; set; }
    public int FraudScore { get; set; }
    public bool IsSuspicious { get; set; }
}

public class AllowanceSummaryResponseDto
{
    public bool Success { get; set; } = true;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal TotalAllowance { get; set; }
    public List<AllowanceDto> Allowances { get; set; } = new();
}

public class ApproveAllowanceRequest
{
    public bool Approved { get; set; }
    public string? Remarks { get; set; }
}

public class BulkApproveAllowanceRequest
{
    public List<int> Ids { get; set; } = new();
    public bool Approved { get; set; } = true;
    public string? Remarks { get; set; }
}

public class BulkApproveAllowanceResponseDto
{
    public int Count { get; set; }
    public List<int> ApprovedIds { get; set; } = new();
}

// ─── Fraud DTOs ──────────────────────────────────────────────────────────────

public class FraudReportDto
{
    public int SessionId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string SessionDate { get; set; } = string.Empty;
    public int FraudScore { get; set; }
    public bool IsSuspicious { get; set; }
    public List<string> FraudFlags { get; set; } = new();
    public decimal RawDistanceKm { get; set; }
    public decimal FilteredDistanceKm { get; set; }
    public decimal ReconstructedDistanceKm { get; set; }
    public int TotalPings { get; set; }
    public int InvalidPings { get; set; }
    public int FilteredPings { get; set; }
    public int MockedPings { get; set; }
}

// ─── Batch Ping DTO (for offline sync) ───────────────────────────────────────

public class BatchPingRequest
{
    public List<PingRequest> Pings { get; set; } = new();
}

public class BatchPingResponseDto
{
    public bool Success { get; set; } = true;
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public int Filtered { get; set; }
    public decimal CumulativeDistanceKm { get; set; }
    public decimal AllowanceAmount { get; set; }
}
