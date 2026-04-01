using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Tracking;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;
using System.Text.Json;

namespace SalesCRM.Infrastructure.Services;

public class TrackingService : ITrackingService
{
    private readonly IUnitOfWork _uow;
    private readonly ITrackingHubNotifier? _notifier;
    private readonly IGeofenceService? _geofenceService;

    // ─── Configuration constants ─────────────────────────────────────────────
    private const decimal MinMovementKm = 0.005m;   // 5 meters — ignore GPS jitter below this
    private const decimal MaxAccuracyMetres = 50m;   // Reject pings with accuracy worse than 50m
    private const decimal MaxSpeedKmh = 150m;
    private const decimal TeleportThresholdKm = 2m;
    private const int TeleportTimeThresholdSec = 15;
    private const int FraudScoreThreshold = 50;

    public TrackingService(IUnitOfWork uow, ITrackingHubNotifier? notifier = null, IGeofenceService? geofenceService = null)
    {
        _uow = uow;
        _notifier = notifier;
        _geofenceService = geofenceService;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DateTime GetTodayIst()
    {
        var ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        var istDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist).Date;
        return DateTime.SpecifyKind(istDate, DateTimeKind.Utc);
    }

    private static decimal HaversineKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double R = 6371.0;
        double dLat = ToRad((double)(lat2 - lat1));
        double dLon = ToRad((double)(lon2 - lon1));
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad((double)lat1)) * Math.Cos(ToRad((double)lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (decimal)(R * c);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
    private static double ToDeg(double rad) => rad * 180.0 / Math.PI;

    /// <summary>Calculate bearing (heading) in degrees from point 1 to point 2</summary>
    private static decimal CalculateBearing(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        double dLon = ToRad((double)(lon2 - lon1));
        double y = Math.Sin(dLon) * Math.Cos(ToRad((double)lat2));
        double x = Math.Cos(ToRad((double)lat1)) * Math.Sin(ToRad((double)lat2)) -
                   Math.Sin(ToRad((double)lat1)) * Math.Cos(ToRad((double)lat2)) * Math.Cos(dLon);
        double bearing = ToDeg(Math.Atan2(y, x));
        return (decimal)((bearing + 360) % 360);
    }

    private static string StatusToString(TrackingSessionStatus s) => s switch
    {
        TrackingSessionStatus.NotStarted => "not_started",
        TrackingSessionStatus.Active => "active",
        TrackingSessionStatus.Ended => "ended",
        _ => "not_started"
    };

    private static ButtonStateDto GetButtonState(TrackingSessionStatus? status) => status switch
    {
        TrackingSessionStatus.Active => new() { StartDayEnabled = false, EndDayEnabled = true },
        TrackingSessionStatus.Ended => new() { StartDayEnabled = false, EndDayEnabled = false },
        _ => new() { StartDayEnabled = true, EndDayEnabled = false }
    };

    private TrackingSessionDto ToDto(TrackingSession s, int pingCount = 0) => new()
    {
        SessionId = s.Id,
        Status = StatusToString(s.Status),
        StartedAt = s.StartedAt,
        EndedAt = s.EndedAt,
        SessionDate = s.SessionDate.ToString("yyyy-MM-dd"),
        TotalDistanceKm = s.TotalDistanceKm,
        AllowanceAmount = s.AllowanceAmount,
        PingCount = pingCount,
        RawDistanceKm = s.RawDistanceKm,
        FilteredDistanceKm = s.FilteredDistanceKm,
        ReconstructedDistanceKm = s.ReconstructedDistanceKm,
        FraudScore = s.FraudScore,
        IsSuspicious = s.IsSuspicious,
        FraudFlags = DeserializeFraudFlags(s.FraudFlags)
    };

    private static List<string>? DeserializeFraudFlags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<string>>(json); }
        catch { return null; }
    }

    // ─── Validation Engine ───────────────────────────────────────────────────

    private static (bool isValid, string? reason) ValidatePing(PingRequest request)
    {
        if (request.IsMocked)
            return (false, "Mock location detected");

        if (request.AccuracyMetres > MaxAccuracyMetres)
            return (false, $"GPS accuracy too low (>{MaxAccuracyMetres}m)");

        if (request.SpeedKmh > MaxSpeedKmh)
            return (false, $"Speed too high (>{MaxSpeedKmh} km/h)");

        if (request.Latitude < -90 || request.Latitude > 90 || request.Longitude < -180 || request.Longitude > 180)
            return (false, "Invalid coordinates");

        return (true, null);
    }

    private static (bool isTeleport, string? reason) CheckTeleport(decimal distanceKm, TimeSpan timeDiff)
    {
        if (distanceKm > TeleportThresholdKm && timeDiff.TotalSeconds < TeleportTimeThresholdSec)
            return (true, $"Teleport detected ({distanceKm:F2}km in {timeDiff.TotalSeconds:F0}s)");
        return (false, null);
    }

    // ─── Noise Filter (server-side, mirrors client-side logic) ───────────────

    private static (bool isFiltered, string? reason) ShouldFilter(decimal distanceKm, LocationPing? prevPing, PingRequest request)
    {
        // Ignore micro-movements (GPS jitter) < 5 meters
        if (prevPing != null && distanceKm < MinMovementKm)
            return (true, $"Movement below threshold (<{MinMovementKm * 1000:F0}m)");

        // Filter pings with very poor accuracy — distance from these is unreliable
        if (request.AccuracyMetres.HasValue && request.AccuracyMetres > 30m && distanceKm < request.AccuracyMetres / 1000m)
            return (true, $"Distance ({distanceKm * 1000:F0}m) within accuracy margin ({request.AccuracyMetres:F0}m)");

        return (false, null);
    }

    // ─── Fraud Detection Engine ──────────────────────────────────────────────

    private async Task<(int score, List<string> flags)> CalculateFraudScoreAsync(int sessionId)
    {
        var pings = await _uow.LocationPings.Query()
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.RecordedAt)
            .ToListAsync();

        int score = 0;
        var flags = new List<string>();

        if (pings.Count == 0) return (0, flags);

        // 1. Mock location pings
        int mockedCount = pings.Count(p => p.IsMocked);
        if (mockedCount > 0)
        {
            score += 40;
            flags.Add($"mock_locations:{mockedCount}");
        }

        // 2. Teleport jumps (invalid due to distance)
        int teleportCount = pings.Count(p => !p.IsValid && (p.InvalidReason?.Contains("Teleport") == true));
        if (teleportCount > 2)
        {
            score += 20;
            flags.Add($"teleport_jumps:{teleportCount}");
        }

        // 3. Constant perfect speed (suspiciously uniform)
        var validPings = pings.Where(p => p.IsValid && p.SpeedKmh.HasValue && p.SpeedKmh > 0).ToList();
        if (validPings.Count >= 10)
        {
            var speeds = validPings.Select(p => (double)p.SpeedKmh!.Value).ToList();
            double avgSpeed = speeds.Average();
            double variance = speeds.Sum(s => (s - avgSpeed) * (s - avgSpeed)) / speeds.Count;
            if (variance < 0.5 && avgSpeed > 5) // Almost no speed variation while "moving"
            {
                score += 15;
                flags.Add("constant_speed_pattern");
            }
        }

        // 4. High ratio of invalid pings
        int invalidCount = pings.Count(p => !p.IsValid);
        double invalidRatio = (double)invalidCount / pings.Count;
        if (invalidRatio > 0.3 && pings.Count > 10)
        {
            score += 10;
            flags.Add($"high_invalid_ratio:{invalidRatio:P0}");
        }

        // 5. Too many filtered pings (stationary but session active for hours)
        int filteredCount = pings.Count(p => p.IsFiltered);
        if (filteredCount > 0 && pings.Count > 20)
        {
            double filteredRatio = (double)filteredCount / pings.Count;
            if (filteredRatio > 0.8) // 80%+ stationary
            {
                score += 10;
                flags.Add("mostly_stationary");
            }
        }

        // 6. Provider switches (constantly switching GPS providers)
        var providerSwitches = 0;
        for (int i = 1; i < pings.Count; i++)
        {
            if (pings[i].Provider != pings[i - 1].Provider && pings[i].Provider != null && pings[i - 1].Provider != null)
                providerSwitches++;
        }
        if (providerSwitches > pings.Count / 3 && pings.Count > 10)
        {
            score += 5;
            flags.Add($"provider_switching:{providerSwitches}");
        }

        return (Math.Min(score, 100), flags);
    }

    // ─── Path Reconstruction Engine ──────────────────────────────────────────

    private async Task<(decimal reconstructedDistance, List<LocationPing> reconstructedPings)>
        ReconstructPathAsync(int sessionId)
    {
        // Get all valid, non-filtered pings in chronological order
        var pings = await _uow.LocationPings.Query()
            .Where(p => p.SessionId == sessionId && p.IsValid && !p.IsFiltered)
            .OrderBy(p => p.RecordedAt)
            .ToListAsync();

        if (pings.Count < 2)
            return (0, pings);

        // Step 1: Light simplification — only remove GPS noise, preserve real path
        var simplified = SimplifyPath(pings, 0.005m); // 5 meter tolerance

        // Step 2: Calculate total distance along simplified path (sum all segments)
        decimal totalDistance = 0;
        for (int i = 1; i < simplified.Count; i++)
        {
            decimal segDist = HaversineKm(
                simplified[i - 1].Latitude, simplified[i - 1].Longitude,
                simplified[i].Latitude, simplified[i].Longitude);

            if (segDist >= MinMovementKm)
                totalDistance += segDist;
        }

        return (totalDistance, simplified);
    }

    /// <summary>
    /// Simplified Ramer-Douglas-Peucker path simplification using perpendicular distance
    /// </summary>
    private static List<LocationPing> SimplifyPath(List<LocationPing> points, decimal toleranceKm)
    {
        if (points.Count <= 2) return points;

        // Find the point with the maximum distance from the line between first and last
        decimal maxDist = 0;
        int index = 0;

        var first = points[0];
        var last = points[^1];

        for (int i = 1; i < points.Count - 1; i++)
        {
            decimal dist = PerpendicularDistanceKm(points[i], first, last);
            if (dist > maxDist)
            {
                maxDist = dist;
                index = i;
            }
        }

        if (maxDist > toleranceKm)
        {
            // Recursively simplify both halves
            var left = SimplifyPath(points.Take(index + 1).ToList(), toleranceKm);
            var right = SimplifyPath(points.Skip(index).ToList(), toleranceKm);

            // Combine, avoiding duplicate at junction
            var result = new List<LocationPing>(left);
            result.AddRange(right.Skip(1));
            return result;
        }
        else
        {
            // All intermediate points are within tolerance — keep only endpoints
            return new List<LocationPing> { first, last };
        }
    }

    private static decimal PerpendicularDistanceKm(LocationPing point, LocationPing lineStart, LocationPing lineEnd)
    {
        // Approximate perpendicular distance using cross-product method
        decimal dAP = HaversineKm(lineStart.Latitude, lineStart.Longitude, point.Latitude, point.Longitude);
        decimal dAB = HaversineKm(lineStart.Latitude, lineStart.Longitude, lineEnd.Latitude, lineEnd.Longitude);

        if (dAB == 0) return dAP;

        // Use bearing-based perpendicular distance approximation
        decimal dBP = HaversineKm(lineEnd.Latitude, lineEnd.Longitude, point.Latitude, point.Longitude);

        // Heron's formula to get area, then height = 2*area/base
        decimal s = (dAP + dBP + dAB) / 2;
        decimal areaSquared = s * (s - dAP) * (s - dBP) * (s - dAB);
        if (areaSquared <= 0) return 0;
        decimal area = (decimal)Math.Sqrt((double)areaSquared);
        return 2 * area / dAB;
    }

    // ─── Start Day ───────────────────────────────────────────────────────────

    public async Task<SessionResponseDto> StartDayAsync(int userId, string role)
    {
        var todayIst = GetTodayIst();

        var existing = await _uow.TrackingSessions.Query()
            .Where(s => s.UserId == userId && s.SessionDate.Date == todayIst.Date)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();

        if (existing != null && existing.Status == TrackingSessionStatus.Active)
        {
            var pingCount = await _uow.LocationPings.Query()
                .CountAsync(p => p.SessionId == existing.Id && p.IsValid);
            return new()
            {
                Session = ToDto(existing, pingCount),
                ButtonState = GetButtonState(existing.Status)
            };
        }

        var user = await _uow.Users.GetByIdAsync(userId);
        var rate = user?.TravelAllowanceRate ?? 10.00m;

        if (!Enum.TryParse<UserRole>(role, out var userRole))
            userRole = UserRole.FO;

        var session = new TrackingSession
        {
            UserId = userId,
            Role = userRole,
            SessionDate = todayIst,
            StartedAt = DateTime.UtcNow,
            Status = TrackingSessionStatus.Active,
            AllowanceRatePerKm = rate
        };

        await _uow.TrackingSessions.AddAsync(session);
        await _uow.SaveChangesAsync();

        return new()
        {
            Session = ToDto(session),
            ButtonState = GetButtonState(session.Status)
        };
    }

    // ─── End Day ─────────────────────────────────────────────────────────────

    public async Task<SessionResponseDto> EndDayAsync(int userId)
    {
        var todayIst = GetTodayIst();

        var session = await _uow.TrackingSessions.Query()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionDate.Date == todayIst.Date && s.Status == TrackingSessionStatus.Active);

        if (session == null)
        {
            return new() { Success = false, ButtonState = new() { StartDayEnabled = false, EndDayEnabled = false } };
        }

        // Run path reconstruction to get accurate final distance
        var (reconstructedDist, _) = await ReconstructPathAsync(session.Id);

        // Calculate fraud score
        var (fraudScore, fraudFlags) = await CalculateFraudScoreAsync(session.Id);

        // Get raw distance from cumulative pings
        var lastPing = await _uow.LocationPings.Query()
            .Where(p => p.SessionId == session.Id && p.IsValid)
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync();

        var rawDistance = lastPing?.CumulativeDistanceKm ?? 0;

        // Get filtered distance (valid pings only, excluding filtered ones)
        var filteredPings = await _uow.LocationPings.Query()
            .Where(p => p.SessionId == session.Id && p.IsValid && !p.IsFiltered)
            .OrderBy(p => p.RecordedAt)
            .ToListAsync();

        decimal filteredDistance = 0;
        for (int i = 1; i < filteredPings.Count; i++)
        {
            var d = HaversineKm(filteredPings[i - 1].Latitude, filteredPings[i - 1].Longitude,
                                filteredPings[i].Latitude, filteredPings[i].Longitude);
            if (d >= MinMovementKm) filteredDistance += d;
        }

        // Use filtered distance (real GPS path) for accuracy — reconstructed is kept for reference
        var finalDistance = filteredDistance;
        var allowance = finalDistance * session.AllowanceRatePerKm;

        // Close any open school visits before ending the session
        if (_geofenceService != null)
        {
            try { await _geofenceService.CloseOpenVisitsAsync(session.Id, DateTime.UtcNow); }
            catch { /* best-effort */ }
        }

        session.Status = TrackingSessionStatus.Ended;
        session.EndedAt = DateTime.UtcNow;
        session.RawDistanceKm = rawDistance;
        session.FilteredDistanceKm = filteredDistance;
        session.ReconstructedDistanceKm = reconstructedDist;
        session.TotalDistanceKm = finalDistance;
        session.AllowanceAmount = allowance;
        session.FraudScore = fraudScore;
        session.IsSuspicious = fraudScore >= FraudScoreThreshold;
        session.FraudFlags = fraudFlags.Count > 0 ? JsonSerializer.Serialize(fraudFlags) : null;

        // No need to call UpdateAsync — session is already tracked by EF Core

        // Create daily allowance record
        var existingAllowance = await _uow.DailyAllowances.Query()
            .FirstOrDefaultAsync(a => a.SessionId == session.Id);

        if (existingAllowance == null)
        {
            var dailyAllowance = new DailyAllowance
            {
                SessionId = session.Id,
                UserId = userId,
                AllowanceDate = todayIst,
                TotalDistanceKm = finalDistance,
                RatePerKm = session.AllowanceRatePerKm,
                GrossAllowance = allowance
            };
            await _uow.DailyAllowances.AddAsync(dailyAllowance);
        }

        await _uow.SaveChangesAsync();

        // Emit session_ended via SignalR
        if (_notifier != null)
        {
            var user = await _uow.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                await _notifier.SendSessionEnded(userId, user.ZoneId, user.RegionId);
            }
        }

        return new()
        {
            Session = ToDto(session),
            ButtonState = GetButtonState(session.Status)
        };
    }

    // ─── Get Today Session ───────────────────────────────────────────────────

    public async Task<SessionResponseDto> GetTodaySessionAsync(int userId)
    {
        var todayIst = GetTodayIst();

        var session = await _uow.TrackingSessions.Query()
            .Where(s => s.UserId == userId && s.SessionDate.Date == todayIst.Date)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();

        if (session == null)
        {
            return new()
            {
                Session = new()
                {
                    Status = "not_started",
                    SessionDate = todayIst.ToString("yyyy-MM-dd"),
                    TotalDistanceKm = 0,
                    AllowanceAmount = 0
                },
                ButtonState = GetButtonState(null)
            };
        }

        var pingCount = await _uow.LocationPings.Query()
            .CountAsync(p => p.SessionId == session.Id && p.IsValid);

        return new()
        {
            Session = ToDto(session, pingCount),
            ButtonState = GetButtonState(session.Status)
        };
    }

    // ─── Record Ping ─────────────────────────────────────────────────────────

    public async Task<PingResponseDto> RecordPingAsync(int userId, PingRequest request)
    {
        var todayIst = GetTodayIst();

        var session = await _uow.TrackingSessions.Query()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionDate.Date == todayIst.Date && s.Status == TrackingSessionStatus.Active);

        if (session == null)
        {
            return new() { Success = false, IsValid = false };
        }

        // Normalize RecordedAt to UTC
        var recordedAt = request.RecordedAt ?? DateTime.UtcNow;
        if (recordedAt.Kind != DateTimeKind.Utc)
            recordedAt = DateTime.SpecifyKind(recordedAt, DateTimeKind.Utc);

        // Step 1: Validate ping
        var (isValid, invalidReason) = ValidatePing(request);

        // Step 2: Distance calculation & teleport check
        decimal distanceFromPrev = 0;
        decimal cumulative = 0;
        bool isFiltered = false;
        string? filterReason = null;

        LocationPing? prevPing = null;
        if (isValid)
        {
            prevPing = await _uow.LocationPings.Query()
                .Where(p => p.SessionId == session.Id && p.IsValid && !p.IsFiltered)
                .OrderByDescending(p => p.RecordedAt)
                .FirstOrDefaultAsync();

            if (prevPing != null)
            {
                distanceFromPrev = HaversineKm(prevPing.Latitude, prevPing.Longitude, request.Latitude, request.Longitude);

                // Teleport detection (3km in 30s)
                var timeDiff = recordedAt - prevPing.RecordedAt;
                if (timeDiff.TotalSeconds < 0) timeDiff = TimeSpan.Zero; // guard against clock skew
                var (isTeleport, teleportReason) = CheckTeleport(distanceFromPrev, timeDiff);
                if (isTeleport)
                {
                    isValid = false;
                    invalidReason = teleportReason;
                    distanceFromPrev = 0;
                }
            }

            // Step 3: Noise filter — ignore micro-movements < 15m
            if (isValid)
            {
                var (filtered, fReason) = ShouldFilter(distanceFromPrev, prevPing, request);
                if (filtered)
                {
                    isFiltered = true;
                    filterReason = fReason;
                    distanceFromPrev = 0; // Don't add to cumulative
                }
            }

            // Calculate cumulative from last valid non-filtered ping
            if (prevPing != null)
            {
                cumulative = prevPing.CumulativeDistanceKm + (isValid && !isFiltered ? distanceFromPrev : 0);
            }
        }

        var ping = new LocationPing
        {
            SessionId = session.Id,
            UserId = userId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AccuracyMetres = request.AccuracyMetres,
            SpeedKmh = request.SpeedKmh,
            AltitudeMetres = request.AltitudeMetres,
            DistanceFromPrevKm = distanceFromPrev,
            CumulativeDistanceKm = cumulative,
            RecordedAt = recordedAt,
            IsValid = isValid,
            InvalidReason = invalidReason,
            Provider = request.Provider,
            IsMocked = request.IsMocked,
            BatteryLevel = request.BatteryLevel,
            IsFiltered = isFiltered,
            FilterReason = filterReason
        };

        await _uow.LocationPings.AddAsync(ping);

        // Update session totals — no need to call UpdateAsync since session is already tracked
        if (isValid && !isFiltered)
        {
            session.TotalDistanceKm = cumulative;
            session.AllowanceAmount = cumulative * session.AllowanceRatePerKm;
            session.RawDistanceKm = cumulative;
        }

        await _uow.SaveChangesAsync();

        // Process geofence enter/exit (non-critical — don't fail the ping)
        if (isValid && !isFiltered && _geofenceService != null)
        {
            try
            {
                await _geofenceService.ProcessPingForGeofenceAsync(session.Id, userId, request.Latitude, request.Longitude, ping.RecordedAt);
            }
            catch
            {
                // Geofence processing is best-effort
            }
        }

        // Emit live location via SignalR (non-critical — don't let it fail the ping)
        if (isValid && !isFiltered && _notifier != null)
        {
            try
            {
                var user = await _uow.Users.Query()
                    .Include(u => u.Zone)
                    .Include(u => u.Region)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    var payload = new LiveLocationDto
                    {
                        UserId = userId,
                        Name = user.Name,
                        Role = user.Role.ToString(),
                        ZoneId = user.ZoneId,
                        ZoneName = user.Zone?.Name,
                        RegionId = user.RegionId,
                        RegionName = user.Region?.Name,
                        Latitude = request.Latitude,
                        Longitude = request.Longitude,
                        SpeedKmh = request.SpeedKmh,
                        LastSeen = ping.RecordedAt,
                        TotalDistanceKm = cumulative,
                        AllowanceAmount = cumulative * session.AllowanceRatePerKm,
                        Status = "active",
                        BatteryLevel = request.BatteryLevel,
                        FraudScore = session.FraudScore,
                        IsSuspicious = session.IsSuspicious
                    };

                    await _notifier.SendLiveLocation(payload, user.ZoneId, user.RegionId);
                }
            }
            catch
            {
                // SignalR notification is best-effort; don't fail the ping save
            }
        }

        return new()
        {
            PingId = ping.Id,
            IsValid = isValid,
            IsFiltered = isFiltered,
            FilterReason = filterReason,
            CumulativeDistanceKm = cumulative,
            AllowanceAmount = cumulative * session.AllowanceRatePerKm,
            FraudScore = session.FraudScore
        };
    }

    // ─── Batch Ping (Offline Sync) ───────────────────────────────────────────

    public async Task<BatchPingResponseDto> RecordBatchPingsAsync(int userId, BatchPingRequest request)
    {
        if (request.Pings == null || request.Pings.Count == 0)
            return new() { Success = false };

        // Sort pings by timestamp
        var sortedPings = request.Pings
            .OrderBy(p => p.RecordedAt ?? DateTime.UtcNow)
            .ToList();

        int accepted = 0, rejected = 0, filtered = 0;
        decimal lastCumulative = 0;
        decimal lastAllowance = 0;

        foreach (var ping in sortedPings)
        {
            try
            {
                var result = await RecordPingAsync(userId, ping);
                if (!result.Success) continue;

                if (!result.IsValid) rejected++;
                else if (result.IsFiltered) filtered++;
                else accepted++;

                lastCumulative = result.CumulativeDistanceKm;
                lastAllowance = result.AllowanceAmount;
            }
            catch
            {
                rejected++;
                // Continue processing remaining pings even if one fails
            }
        }

        return new()
        {
            Accepted = accepted,
            Rejected = rejected,
            Filtered = filtered,
            CumulativeDistanceKm = lastCumulative,
            AllowanceAmount = lastAllowance
        };
    }

    // ─── Live Locations ──────────────────────────────────────────────────────

    public async Task<List<LiveLocationDto>> GetLiveLocationsAsync(int userId, string role, string? filterRole = null)
    {
        var todayIst = GetTodayIst();
        var user = await _uow.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new();

        // Build users query based on requester's role
        IQueryable<User> usersQuery = _uow.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region);

        if (role == "SCA")
        {
            // SCA can track all roles — optionally filter by a specific role
            if (!string.IsNullOrEmpty(filterRole) && Enum.TryParse<UserRole>(filterRole, true, out var targetRole))
                usersQuery = usersQuery.Where(u => u.Role == targetRole);
            else
                usersQuery = usersQuery.Where(u => u.Role != UserRole.SCA);
        }
        else if (role == "FO")
        {
            usersQuery = usersQuery.Where(u => u.Id == userId);
        }
        else
        {
            // ZH, RH, SH — default to seeing FOs in their scope
            usersQuery = usersQuery.Where(u => u.Role == UserRole.FO);

            if (role == "ZH")
            {
                if (user.ZoneId.HasValue)
                    usersQuery = usersQuery.Where(u => u.ZoneId == user.ZoneId);
                else
                    usersQuery = usersQuery.Where(u => false);
            }
            else if (role == "RH")
            {
                if (user.RegionId.HasValue)
                    usersQuery = usersQuery.Where(u => u.RegionId == user.RegionId);
                else
                    usersQuery = usersQuery.Where(u => false);
            }
            // SH sees all FOs
        }

        var scopedUsers = await usersQuery.ToListAsync();

        // Get today's sessions for these users
        var scopedUserIds = scopedUsers.Select(u => u.Id).ToList();
        var sessions = await _uow.TrackingSessions.Query()
            .Where(s => scopedUserIds.Contains(s.UserId) && s.SessionDate.Date == todayIst.Date)
            .ToListAsync();

        var sessionByUser = sessions.GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartedAt).First());

        var result = new List<LiveLocationDto>();

        foreach (var fo in scopedUsers)
        {
            sessionByUser.TryGetValue(fo.Id, out var session);

            LocationPing? lastPing = null;
            LocationPing? prevPing = null;
            bool isLastKnownLocation = false;
            string? lastSessionDate = null;

            if (session != null)
            {
                var recentPings = await _uow.LocationPings.Query()
                    .Where(p => p.SessionId == session.Id && p.IsValid && !p.IsFiltered)
                    .OrderByDescending(p => p.RecordedAt)
                    .Take(2)
                    .ToListAsync();

                lastPing = recentPings.Count > 0 ? recentPings[0] : null;
                prevPing = recentPings.Count > 1 ? recentPings[1] : null;
            }

            // Fallback: if no today session or no pings today, get last known location from most recent past session
            if (lastPing == null)
            {
                var lastSession = await _uow.TrackingSessions.Query()
                    .Where(s => s.UserId == fo.Id && s.SessionDate.Date < todayIst.Date)
                    .OrderByDescending(s => s.SessionDate)
                    .FirstOrDefaultAsync();

                if (lastSession != null)
                {
                    var pastPings = await _uow.LocationPings.Query()
                        .Where(p => p.SessionId == lastSession.Id && p.IsValid && !p.IsFiltered)
                        .OrderByDescending(p => p.RecordedAt)
                        .Take(2)
                        .ToListAsync();

                    if (pastPings.Count > 0)
                    {
                        lastPing = pastPings[0];
                        prevPing = pastPings.Count > 1 ? pastPings[1] : null;
                        isLastKnownLocation = true;
                        lastSessionDate = lastSession.SessionDate.ToString("yyyy-MM-dd");
                    }
                }
            }

            // Calculate heading (bearing) from previous to current ping
            decimal? heading = null;
            if (lastPing != null && prevPing != null)
            {
                heading = CalculateBearing(prevPing.Latitude, prevPing.Longitude, lastPing.Latitude, lastPing.Longitude);
            }

            // Check if FO is currently inside a school geofence
            int? currentSchoolId = null;
            string? currentSchoolName = null;
            if (session != null)
            {
                var openVisit = await _uow.SchoolVisitLogs.Query()
                    .Include(v => v.School)
                    .Where(v => v.SessionId == session.Id && v.ExitedAt == null)
                    .FirstOrDefaultAsync();
                if (openVisit != null)
                {
                    currentSchoolId = openVisit.SchoolId;
                    currentSchoolName = openVisit.School?.Name;
                }
            }

            result.Add(new LiveLocationDto
            {
                UserId = fo.Id,
                Name = fo.Name ?? "",
                Role = fo.Role.ToString(),
                ZoneId = fo.ZoneId,
                ZoneName = fo.Zone?.Name,
                RegionId = fo.RegionId,
                RegionName = fo.Region?.Name,
                Latitude = lastPing?.Latitude ?? 0,
                Longitude = lastPing?.Longitude ?? 0,
                SpeedKmh = lastPing?.SpeedKmh,
                LastSeen = lastPing?.RecordedAt ?? session?.StartedAt ?? DateTime.UtcNow,
                TotalDistanceKm = session?.TotalDistanceKm ?? 0,
                AllowanceAmount = session?.AllowanceAmount ?? 0,
                Status = session != null ? StatusToString(session.Status) : "not_started",
                FraudScore = session?.FraudScore ?? 0,
                IsSuspicious = session?.IsSuspicious ?? false,
                BatteryLevel = lastPing?.BatteryLevel,
                Heading = heading,
                CurrentSchoolId = currentSchoolId,
                CurrentSchoolName = currentSchoolName,
                IsLastKnownLocation = isLastKnownLocation,
                LastSessionDate = isLastKnownLocation ? lastSessionDate : todayIst.Date.ToString("yyyy-MM-dd")
            });
        }

        return result;
    }

    // ─── Route ───────────────────────────────────────────────────────────────

    public async Task<RouteResponseDto> GetRouteAsync(int requesterId, string requesterRole, int targetUserId, string date)
    {
        if (!DateTime.TryParse(date, out var parsedDate))
            return new() { Success = false };

        var dateOnly = parsedDate.Date;

        // Check scope access — FO can only see their own route
        if (requesterRole == "FO" && requesterId != targetUserId)
            return new() { Success = false };

        // SCA can see all routes — skip scope check
        if (requesterRole == "SCA")
        {
            var targetUser2 = await _uow.Users.GetByIdAsync(targetUserId);
            if (targetUser2 == null) return new() { Success = false };
        }

        var targetUser = await _uow.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == targetUserId);

        if (targetUser == null) return new() { Success = false };

        // Scope check — ZH can see FOs in same zone, RH in same region, SH/SCA sees all
        if (requesterRole == "ZH")
        {
            var requester = await _uow.Users.GetByIdAsync(requesterId);
            // Allow if both are in the same zone, OR if either has no zone assigned (permissive)
            if (requester?.ZoneId != null && targetUser.ZoneId != null && requester.ZoneId != targetUser.ZoneId)
                return new() { Success = false };
        }
        else if (requesterRole == "RH")
        {
            var requester = await _uow.Users.GetByIdAsync(requesterId);
            if (requester?.RegionId != null && targetUser.RegionId != null && requester.RegionId != targetUser.RegionId)
                return new() { Success = false };
        }

        // Use .Date comparison to avoid UTC/Unspecified kind mismatch
        var sessions = await _uow.TrackingSessions.Query()
            .Where(s => s.UserId == targetUserId)
            .ToListAsync();

        var session = sessions
            .Where(s => s.SessionDate.Date == dateOnly)
            .OrderByDescending(s => s.Status == TrackingSessionStatus.Active ? 1 : 0)
            .ThenByDescending(s => s.StartedAt)
            .FirstOrDefault();

        // No session for this date — return empty route instead of failing
        if (session == null)
        {
            return new()
            {
                User = new RouteUserDto { Id = targetUser.Id, Name = targetUser.Name ?? "", Role = targetUser.Role.ToString() },
                Session = new TrackingSessionDto { Status = "not_started", SessionDate = date, TotalDistanceKm = 0, AllowanceAmount = 0 },
                Route = new(), ReconstructedRoute = new()
            };
        }

        // Raw route (all valid pings)
        var allPings = await _uow.LocationPings.Query()
            .Where(p => p.SessionId == session.Id && p.IsValid)
            .OrderBy(p => p.RecordedAt)
            .ToListAsync();

        var rawRoute = allPings.Select(p => new RoutePointDto
        {
            Lat = p.Latitude,
            Lon = p.Longitude,
            RecordedAt = p.RecordedAt,
            SpeedKmh = p.SpeedKmh,
            IsFiltered = p.IsFiltered,
            ClusterGroup = p.ClusterGroup
        }).ToList();

        // Reconstructed route (valid + non-filtered, simplified)
        var (_, reconstructedPings) = await ReconstructPathAsync(session.Id);
        var reconstructedRoute = reconstructedPings.Select(p => new RoutePointDto
        {
            Lat = p.Latitude,
            Lon = p.Longitude,
            RecordedAt = p.RecordedAt,
            SpeedKmh = p.SpeedKmh,
            IsFiltered = false,
            ClusterGroup = p.ClusterGroup
        }).ToList();

        return new()
        {
            User = new RouteUserDto
            {
                Id = targetUser.Id,
                Name = targetUser.Name,
                Role = targetUser.Role.ToString()
            },
            Session = ToDto(session, allPings.Count),
            Route = rawRoute,
            ReconstructedRoute = reconstructedRoute
        };
    }

    // ─── Allowances ──────────────────────────────────────────────────────────

    public async Task<AllowanceSummaryResponseDto> GetAllowancesAsync(int userId, string role, string from, string to)
    {
        if (!DateTime.TryParse(from, out var fromDate) || !DateTime.TryParse(to, out var toDate))
            return new() { Success = false };

        fromDate = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        toDate = DateTime.SpecifyKind(toDate.Date, DateTimeKind.Utc);

        var user = await _uow.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new() { Success = false };

        var query = _uow.DailyAllowances.Query()
            .Include(a => a.User)
            .Include(a => a.ApprovedBy)
            .Include(a => a.Session)
            .Where(a => a.AllowanceDate.Date >= fromDate.Date && a.AllowanceDate.Date <= toDate.Date);

        if (role == "FO")
        {
            query = query.Where(a => a.UserId == userId);
        }
        else if (role == "ZH")
        {
            query = query.Where(a => a.User!.ZoneId == user.ZoneId);
        }
        else if (role == "RH")
        {
            query = query.Where(a => a.User!.RegionId == user.RegionId);
        }
        // SH sees all

        var allowances = await query
            .OrderByDescending(a => a.AllowanceDate)
            .ToListAsync();

        var dtos = allowances.Select(a => new AllowanceDto
        {
            Id = a.Id,
            UserId = a.UserId,
            UserName = a.User?.Name ?? "",
            Role = a.User?.Role.ToString() ?? "",
            AllowanceDate = a.AllowanceDate.ToString("yyyy-MM-dd"),
            DistanceKm = a.TotalDistanceKm,
            RatePerKm = a.RatePerKm,
            GrossAmount = a.GrossAllowance,
            Approved = a.Approved,
            ApprovedByName = a.ApprovedBy?.Name,
            ApprovedAt = a.ApprovedAt,
            Remarks = a.Remarks,
            RawDistanceKm = a.Session?.RawDistanceKm ?? 0,
            FilteredDistanceKm = a.Session?.FilteredDistanceKm ?? 0,
            FraudScore = a.Session?.FraudScore ?? 0,
            IsSuspicious = a.Session?.IsSuspicious ?? false
        }).ToList();

        return new()
        {
            From = fromDate.ToString("yyyy-MM-dd"),
            To = toDate.ToString("yyyy-MM-dd"),
            TotalAllowance = dtos.Sum(d => d.GrossAmount),
            Allowances = dtos
        };
    }

    // ─── Approve Allowance ───────────────────────────────────────────────────

    public async Task<AllowanceDto> ApproveAllowanceAsync(int approverId, int allowanceId, ApproveAllowanceRequest request)
    {
        var allowance = await _uow.DailyAllowances.Query()
            .Include(a => a.User)
            .Include(a => a.Session)
            .FirstOrDefaultAsync(a => a.Id == allowanceId);

        if (allowance == null)
            throw new InvalidOperationException("Allowance not found");

        allowance.Approved = request.Approved;
        allowance.ApprovedById = approverId;
        allowance.ApprovedAt = DateTime.UtcNow;
        allowance.Remarks = request.Remarks;

        await _uow.DailyAllowances.UpdateAsync(allowance);
        await _uow.SaveChangesAsync();

        var approver = await _uow.Users.GetByIdAsync(approverId);

        return new AllowanceDto
        {
            Id = allowance.Id,
            UserId = allowance.UserId,
            UserName = allowance.User?.Name ?? "",
            Role = allowance.User?.Role.ToString() ?? "",
            AllowanceDate = allowance.AllowanceDate.ToString("yyyy-MM-dd"),
            DistanceKm = allowance.TotalDistanceKm,
            RatePerKm = allowance.RatePerKm,
            GrossAmount = allowance.GrossAllowance,
            Approved = allowance.Approved,
            ApprovedByName = approver?.Name,
            ApprovedAt = allowance.ApprovedAt,
            Remarks = allowance.Remarks,
            RawDistanceKm = allowance.Session?.RawDistanceKm ?? 0,
            FilteredDistanceKm = allowance.Session?.FilteredDistanceKm ?? 0,
            FraudScore = allowance.Session?.FraudScore ?? 0,
            IsSuspicious = allowance.Session?.IsSuspicious ?? false
        };
    }

    // ─── Fraud Reports ───────────────────────────────────────────────────────

    public async Task<List<FraudReportDto>> GetFraudReportsAsync(int userId, string role, string from, string to)
    {
        if (!DateTime.TryParse(from, out var fromDate) || !DateTime.TryParse(to, out var toDate))
            return new();

        fromDate = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        toDate = DateTime.SpecifyKind(toDate.Date, DateTimeKind.Utc);

        var user = await _uow.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return new();

        var query = _uow.TrackingSessions.Query()
            .Include(s => s.User)
            .Where(s => s.SessionDate.Date >= fromDate.Date && s.SessionDate.Date <= toDate.Date && s.IsSuspicious);

        if (role == "ZH")
            query = query.Where(s => s.User!.ZoneId == user.ZoneId);
        else if (role == "RH")
            query = query.Where(s => s.User!.RegionId == user.RegionId);
        else if (role == "FO")
            query = query.Where(s => s.UserId == userId);

        var sessions = await query.OrderByDescending(s => s.FraudScore).ToListAsync();
        var reports = new List<FraudReportDto>();

        foreach (var s in sessions)
        {
            var pings = await _uow.LocationPings.Query()
                .Where(p => p.SessionId == s.Id)
                .ToListAsync();

            reports.Add(new FraudReportDto
            {
                SessionId = s.Id,
                UserId = s.UserId,
                UserName = s.User?.Name ?? "",
                SessionDate = s.SessionDate.ToString("yyyy-MM-dd"),
                FraudScore = s.FraudScore,
                IsSuspicious = s.IsSuspicious,
                FraudFlags = DeserializeFraudFlags(s.FraudFlags) ?? new(),
                RawDistanceKm = s.RawDistanceKm,
                FilteredDistanceKm = s.FilteredDistanceKm,
                ReconstructedDistanceKm = s.ReconstructedDistanceKm,
                TotalPings = pings.Count,
                InvalidPings = pings.Count(p => !p.IsValid),
                FilteredPings = pings.Count(p => p.IsFiltered),
                MockedPings = pings.Count(p => p.IsMocked)
            });
        }

        return reports;
    }

    // ─── Close Stale Sessions (Midnight Reset) ──────────────────────────────

    public async Task CloseStaleSessionsAsync()
    {
        var todayIst = GetTodayIst();

        var staleSessions = await _uow.TrackingSessions.Query()
            .Where(s => s.Status == TrackingSessionStatus.Active && s.SessionDate.Date < todayIst.Date)
            .ToListAsync();

        foreach (var session in staleSessions)
        {
            // Run path reconstruction for accurate final distance
            var (reconstructedDist, _) = await ReconstructPathAsync(session.Id);
            var (fraudScore, fraudFlags) = await CalculateFraudScoreAsync(session.Id);

            var lastPing = await _uow.LocationPings.Query()
                .Where(p => p.SessionId == session.Id && p.IsValid)
                .OrderByDescending(p => p.RecordedAt)
                .FirstOrDefaultAsync();

            var rawDistance = lastPing?.CumulativeDistanceKm ?? 0;
            var allowance = reconstructedDist * session.AllowanceRatePerKm;

            session.Status = TrackingSessionStatus.Ended;
            session.EndedAt = DateTime.UtcNow;
            session.RawDistanceKm = rawDistance;
            session.ReconstructedDistanceKm = reconstructedDist;
            session.TotalDistanceKm = reconstructedDist;
            session.AllowanceAmount = allowance;
            session.FraudScore = fraudScore;
            session.IsSuspicious = fraudScore >= FraudScoreThreshold;
            session.FraudFlags = fraudFlags.Count > 0 ? JsonSerializer.Serialize(fraudFlags) : null;

            // No need to call UpdateAsync — session is already tracked by EF Core

            var existingAllowance = await _uow.DailyAllowances.Query()
                .FirstOrDefaultAsync(a => a.SessionId == session.Id);

            if (existingAllowance == null)
            {
                await _uow.DailyAllowances.AddAsync(new DailyAllowance
                {
                    SessionId = session.Id,
                    UserId = session.UserId,
                    AllowanceDate = session.SessionDate,
                    TotalDistanceKm = reconstructedDist,
                    RatePerKm = session.AllowanceRatePerKm,
                    GrossAllowance = allowance
                });
            }
        }

        if (staleSessions.Count > 0)
            await _uow.SaveChangesAsync();
    }
}
