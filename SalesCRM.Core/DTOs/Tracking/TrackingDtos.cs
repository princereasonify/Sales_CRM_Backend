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
    public int PingCount { get; set; }
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

// ─── Ping DTOs ───────────────────────────────────────────────────────────────

public class PingRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? AccuracyMetres { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? AltitudeMetres { get; set; }
    public DateTime? RecordedAt { get; set; }
}

public class PingResponseDto
{
    public bool Success { get; set; } = true;
    public int PingId { get; set; }
    public bool IsValid { get; set; }
    public decimal CumulativeDistanceKm { get; set; }
    public decimal AllowanceAmount { get; set; }
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
}

// ─── Route DTOs ──────────────────────────────────────────────────────────────

public class RoutePointDto
{
    public decimal Lat { get; set; }
    public decimal Lon { get; set; }
    public DateTime RecordedAt { get; set; }
    public decimal? SpeedKmh { get; set; }
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
