using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class GoogleRoadsService : IGoogleRoadsService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GoogleRoadsService> _logger;

    public GoogleRoadsService(HttpClient http, IConfiguration config, ILogger<GoogleRoadsService> logger)
    {
        _http = http;
        _apiKey = config["GoogleMaps:ApiKey"] ?? "";
        _logger = logger;
    }

    /// <summary>
    /// Snap GPS points to nearest roads using Google Roads API.
    /// Max 100 points per request.
    /// </summary>
    public async Task<List<SnappedPoint>> SnapToRoadsAsync(List<(decimal lat, decimal lon)> points)
    {
        if (string.IsNullOrEmpty(_apiKey) || points.Count == 0)
            return new();

        var result = new List<SnappedPoint>();

        // Roads API max 100 points per request — batch if needed
        for (int i = 0; i < points.Count; i += 100)
        {
            var batch = points.Skip(i).Take(100).ToList();
            var path = string.Join("|", batch.Select(p => $"{p.lat:F7},{p.lon:F7}"));
            var url = $"https://roads.googleapis.com/v1/snapToRoads?path={path}&interpolate=true&key={_apiKey}";

            try
            {
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Roads API returned {Status}", resp.StatusCode);
                    continue;
                }

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("snappedPoints", out var snapped))
                {
                    foreach (var sp in snapped.EnumerateArray())
                    {
                        var loc = sp.GetProperty("location");
                        var lat = loc.GetProperty("latitude").GetDecimal();
                        var lon = loc.GetProperty("longitude").GetDecimal();
                        var origIdx = sp.TryGetProperty("originalIndex", out var oi) ? oi.GetInt32() + i : -1;

                        result.Add(new SnappedPoint
                        {
                            Latitude = lat,
                            Longitude = lon,
                            OriginalIndex = origIdx
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Roads API snap failed for batch starting at {Index}", i);
            }
        }

        return result;
    }

    /// <summary>
    /// Get actual road distance between two points using Google Directions API.
    /// Supports up to 25 waypoints.
    /// </summary>
    public async Task<RoadDistanceResult> GetRoadDistanceAsync(
        decimal originLat, decimal originLon,
        decimal destLat, decimal destLon,
        List<(decimal lat, decimal lon)>? waypoints = null)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return new RoadDistanceResult { Success = false, Error = "API key not configured" };

        try
        {
            var origin = $"{originLat:F7},{originLon:F7}";
            var dest = $"{destLat:F7},{destLon:F7}";
            var url = $"https://maps.googleapis.com/maps/api/directions/json?origin={origin}&destination={dest}&mode=driving&key={_apiKey}";

            // Add waypoints (max 25)
            if (waypoints?.Count > 0)
            {
                var wps = waypoints.Take(25).Select(w => $"{w.lat:F7},{w.lon:F7}");
                url += $"&waypoints={string.Join("|", wps)}";
            }

            var resp = await _http.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var status = doc.RootElement.GetProperty("status").GetString();
            if (status != "OK")
                return new RoadDistanceResult { Success = false, Error = $"Directions API: {status}" };

            var legs = doc.RootElement.GetProperty("routes")[0].GetProperty("legs");
            decimal totalDistanceMeters = 0;
            decimal totalDurationSeconds = 0;

            foreach (var leg in legs.EnumerateArray())
            {
                totalDistanceMeters += leg.GetProperty("distance").GetProperty("value").GetDecimal();
                totalDurationSeconds += leg.GetProperty("duration").GetProperty("value").GetDecimal();
            }

            return new RoadDistanceResult
            {
                Success = true,
                DistanceKm = Math.Round(totalDistanceMeters / 1000, 3),
                DurationMinutes = Math.Round(totalDurationSeconds / 60, 1)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Directions API failed");
            return new RoadDistanceResult { Success = false, Error = ex.Message };
        }
    }
}
