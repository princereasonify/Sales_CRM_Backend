namespace SalesCRM.Core.Interfaces;

public class SnappedPoint
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int OriginalIndex { get; set; }
}

public class RoadDistanceResult
{
    public decimal DistanceKm { get; set; }
    public decimal DurationMinutes { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public interface IGoogleRoadsService
{
    Task<List<SnappedPoint>> SnapToRoadsAsync(List<(decimal lat, decimal lon)> points);
    Task<RoadDistanceResult> GetRoadDistanceAsync(decimal originLat, decimal originLon, decimal destLat, decimal destLon, List<(decimal lat, decimal lon)>? waypoints = null);
}
