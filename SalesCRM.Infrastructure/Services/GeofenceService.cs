using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Geofence;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class GeofenceService : IGeofenceService
{
    private readonly IUnitOfWork _uow;

    public GeofenceService(IUnitOfWork uow)
    {
        _uow = uow;
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

    // ─── Core: Process every GPS ping for geofence enter/exit ─────────────────

    public async Task ProcessPingForGeofenceAsync(int sessionId, int userId, decimal latitude, decimal longitude, DateTime recordedAt)
    {
        // Load all active schools (with bounding box pre-filter for performance)
        var degOffset = 0.05m; // ~5.5 km rough bounding box
        var nearbySchools = await _uow.Schools.Query()
            .Where(s => s.IsActive &&
                        s.Latitude >= latitude - degOffset && s.Latitude <= latitude + degOffset &&
                        s.Longitude >= longitude - degOffset && s.Longitude <= longitude + degOffset)
            .ToListAsync();

        // Get all currently open visit logs for this session
        var openVisits = await _uow.SchoolVisitLogs.Query()
            .Where(v => v.SessionId == sessionId && v.ExitedAt == null)
            .ToListAsync();

        var openVisitSchoolIds = openVisits.Select(v => v.SchoolId).ToHashSet();

        foreach (var school in nearbySchools)
        {
            var distanceKm = HaversineKm(latitude, longitude, school.Latitude, school.Longitude);
            var distanceMetres = distanceKm * 1000m;
            var isInside = distanceMetres <= school.GeofenceRadiusMetres;

            if (isInside && !openVisitSchoolIds.Contains(school.Id))
            {
                // ─── ENTER: FO just entered this school's geofence ───
                var enterEvent = new GeofenceEvent
                {
                    SessionId = sessionId,
                    UserId = userId,
                    SchoolId = school.Id,
                    EventType = GeofenceEventType.Enter,
                    Latitude = latitude,
                    Longitude = longitude,
                    DistanceFromSchoolMetres = distanceMetres,
                    RecordedAt = recordedAt
                };
                await _uow.GeofenceEvents.AddAsync(enterEvent);
                await _uow.SaveChangesAsync(); // save to get enterEvent.Id

                var visitLog = new SchoolVisitLog
                {
                    SessionId = sessionId,
                    UserId = userId,
                    SchoolId = school.Id,
                    EnterEventId = enterEvent.Id,
                    EnteredAt = recordedAt,
                    VisitDate = DateTime.SpecifyKind(recordedAt.Date, DateTimeKind.Utc)
                };
                await _uow.SchoolVisitLogs.AddAsync(visitLog);
                await _uow.SaveChangesAsync();
            }
            else if (!isInside && openVisitSchoolIds.Contains(school.Id))
            {
                // ─── EXIT: FO just left this school's geofence ───
                var openVisit = openVisits.First(v => v.SchoolId == school.Id);

                var exitEvent = new GeofenceEvent
                {
                    SessionId = sessionId,
                    UserId = userId,
                    SchoolId = school.Id,
                    EventType = GeofenceEventType.Exit,
                    Latitude = latitude,
                    Longitude = longitude,
                    DistanceFromSchoolMetres = distanceMetres,
                    RecordedAt = recordedAt
                };
                await _uow.GeofenceEvents.AddAsync(exitEvent);
                await _uow.SaveChangesAsync();

                openVisit.ExitEventId = exitEvent.Id;
                openVisit.ExitedAt = recordedAt;
                openVisit.DurationMinutes = (decimal)(recordedAt - openVisit.EnteredAt).TotalMinutes;
                await _uow.SaveChangesAsync();
            }
        }

        // Also check: if FO has open visits for schools NOT in nearbySchools (moved far away)
        var nearbySchoolIds = nearbySchools.Select(s => s.Id).ToHashSet();
        foreach (var openVisit in openVisits.Where(v => !nearbySchoolIds.Contains(v.SchoolId)))
        {
            // FO moved far from school — auto-close
            var exitEvent = new GeofenceEvent
            {
                SessionId = sessionId,
                UserId = userId,
                SchoolId = openVisit.SchoolId,
                EventType = GeofenceEventType.Exit,
                Latitude = latitude,
                Longitude = longitude,
                DistanceFromSchoolMetres = 9999, // beyond range
                RecordedAt = recordedAt
            };
            await _uow.GeofenceEvents.AddAsync(exitEvent);
            await _uow.SaveChangesAsync();

            openVisit.ExitEventId = exitEvent.Id;
            openVisit.ExitedAt = recordedAt;
            openVisit.DurationMinutes = (decimal)(recordedAt - openVisit.EnteredAt).TotalMinutes;
            await _uow.SaveChangesAsync();
        }
    }

    // ─── Close open visits when session ends ──────────────────────────────────

    public async Task CloseOpenVisitsAsync(int sessionId, DateTime exitTime)
    {
        var openVisits = await _uow.SchoolVisitLogs.Query()
            .Where(v => v.SessionId == sessionId && v.ExitedAt == null)
            .ToListAsync();

        foreach (var visit in openVisits)
        {
            visit.ExitedAt = exitTime;
            visit.DurationMinutes = (decimal)(exitTime - visit.EnteredAt).TotalMinutes;
        }

        if (openVisits.Count > 0)
            await _uow.SaveChangesAsync();
    }

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<List<SchoolVisitLogDto>> GetVisitLogsAsync(int userId, string date)
    {
        if (!DateTime.TryParse(date, out var parsedDate)) return new();
        var dateOnly = parsedDate.Date;

        return await _uow.SchoolVisitLogs.Query()
            .Include(v => v.School)
            .Include(v => v.User)
            .Where(v => v.UserId == userId && v.VisitDate.Date == dateOnly)
            .OrderBy(v => v.EnteredAt)
            .Select(v => new SchoolVisitLogDto
            {
                Id = v.Id,
                SessionId = v.SessionId,
                UserId = v.UserId,
                UserName = v.User.Name,
                SchoolId = v.SchoolId,
                SchoolName = v.School.Name,
                EnteredAt = v.EnteredAt,
                ExitedAt = v.ExitedAt,
                DurationMinutes = v.DurationMinutes,
                IsVerified = v.EnterEventId != null && v.ExitEventId != null
            })
            .ToListAsync();
    }

    public async Task<List<SchoolVisitLogDto>> GetVisitLogsBySessionAsync(int sessionId)
    {
        return await _uow.SchoolVisitLogs.Query()
            .Include(v => v.School)
            .Include(v => v.User)
            .Where(v => v.SessionId == sessionId)
            .OrderBy(v => v.EnteredAt)
            .Select(v => new SchoolVisitLogDto
            {
                Id = v.Id,
                SessionId = v.SessionId,
                UserId = v.UserId,
                UserName = v.User.Name,
                SchoolId = v.SchoolId,
                SchoolName = v.School.Name,
                EnteredAt = v.EnteredAt,
                ExitedAt = v.ExitedAt,
                DurationMinutes = v.DurationMinutes,
                IsVerified = v.EnterEventId != null && v.ExitEventId != null
            })
            .ToListAsync();
    }

    public async Task<TimeBreakdownDto> GetTimeBreakdownAsync(int sessionId)
    {
        var session = await _uow.TrackingSessions.GetByIdAsync(sessionId);
        if (session == null) return new TimeBreakdownDto();

        var visitLogs = await GetVisitLogsBySessionAsync(sessionId);

        // Total visit time = sum of all visit durations
        var totalVisitMinutes = visitLogs
            .Where(v => v.DurationMinutes.HasValue)
            .Sum(v => v.DurationMinutes!.Value);

        // Total session duration
        var sessionStart = session.StartedAt ?? session.CreatedAt;
        var sessionEnd = session.EndedAt ?? DateTime.UtcNow;
        var totalSessionMinutes = (decimal)(sessionEnd - sessionStart).TotalMinutes;

        // Idle time: pings where speed < 2 km/h AND not inside any geofence
        // Approximate by looking at filtered (stationary) pings outside geofence windows
        var pings = await _uow.LocationPings.Query()
            .Where(p => p.SessionId == sessionId && p.IsValid)
            .OrderBy(p => p.RecordedAt)
            .ToListAsync();

        decimal idleMinutes = 0;
        foreach (var ping in pings)
        {
            var speed = ping.SpeedKmh ?? 0;
            if (speed >= 2) continue; // moving — not idle

            // Check if this ping's time falls inside any visit window
            var duringVisit = visitLogs.Any(v =>
                ping.RecordedAt >= v.EnteredAt &&
                (v.ExitedAt == null || ping.RecordedAt <= v.ExitedAt));

            if (!duringVisit)
            {
                // Stationary and outside all geofences — count as idle
                // Each ping represents ~25 seconds
                idleMinutes += 0.42m; // ~25 seconds in minutes
            }
        }

        // Travel = total - visit - idle
        var travelMinutes = Math.Max(0, totalSessionMinutes - totalVisitMinutes - idleMinutes);

        return new TimeBreakdownDto
        {
            TotalVisitMinutes = Math.Round(totalVisitMinutes, 1),
            TotalTravelMinutes = Math.Round(travelMinutes, 1),
            TotalIdleMinutes = Math.Round(idleMinutes, 1),
            SchoolsVisitedCount = visitLogs.Select(v => v.SchoolId).Distinct().Count(),
            Visits = visitLogs
        };
    }

    public async Task<List<GeofenceEventDto>> GetGeofenceEventsAsync(int sessionId)
    {
        return await _uow.GeofenceEvents.Query()
            .Include(g => g.School)
            .Where(g => g.SessionId == sessionId)
            .OrderBy(g => g.RecordedAt)
            .Select(g => new GeofenceEventDto
            {
                Id = g.Id,
                SessionId = g.SessionId,
                UserId = g.UserId,
                SchoolId = g.SchoolId,
                SchoolName = g.School.Name,
                EventType = g.EventType.ToString(),
                Latitude = g.Latitude,
                Longitude = g.Longitude,
                DistanceFromSchoolMetres = g.DistanceFromSchoolMetres,
                RecordedAt = g.RecordedAt
            })
            .ToListAsync();
    }
}
