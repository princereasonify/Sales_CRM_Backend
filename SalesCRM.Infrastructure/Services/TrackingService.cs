using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Tracking;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class TrackingService : ITrackingService
{
    private readonly IUnitOfWork _uow;
    private readonly ITrackingHubNotifier? _notifier;

    public TrackingService(IUnitOfWork uow, ITrackingHubNotifier? notifier = null)
    {
        _uow = uow;
        _notifier = notifier;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DateTime GetTodayIst()
    {
        var ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist).Date;
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
        PingCount = pingCount
    };

    // ─── Start Day ───────────────────────────────────────────────────────────

    public async Task<SessionResponseDto> StartDayAsync(int userId, string role)
    {
        var todayIst = GetTodayIst();

        var existing = await _uow.TrackingSessions.Query()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionDate == todayIst);

        if (existing != null)
        {
            if (existing.Status == TrackingSessionStatus.Active)
            {
                var pingCount = await _uow.LocationPings.Query()
                    .CountAsync(p => p.SessionId == existing.Id && p.IsValid);
                return new()
                {
                    Session = ToDto(existing, pingCount),
                    ButtonState = GetButtonState(existing.Status)
                };
            }
            if (existing.Status == TrackingSessionStatus.Ended)
            {
                return new()
                {
                    Success = false,
                    Session = ToDto(existing),
                    ButtonState = GetButtonState(existing.Status)
                };
            }
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
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionDate == todayIst && s.Status == TrackingSessionStatus.Active);

        if (session == null)
        {
            return new() { Success = false, ButtonState = new() { StartDayEnabled = false, EndDayEnabled = false } };
        }

        // Get last valid ping for cumulative distance
        var lastPing = await _uow.LocationPings.Query()
            .Where(p => p.SessionId == session.Id && p.IsValid)
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync();

        var totalDistance = lastPing?.CumulativeDistanceKm ?? 0;
        var allowance = totalDistance * session.AllowanceRatePerKm;

        session.Status = TrackingSessionStatus.Ended;
        session.EndedAt = DateTime.UtcNow;
        session.TotalDistanceKm = totalDistance;
        session.AllowanceAmount = allowance;

        await _uow.TrackingSessions.UpdateAsync(session);

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
                TotalDistanceKm = totalDistance,
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
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionDate == todayIst);

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
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionDate == todayIst && s.Status == TrackingSessionStatus.Active);

        if (session == null)
        {
            return new() { Success = false, IsValid = false };
        }

        // Validate ping
        bool isValid = true;
        string? invalidReason = null;

        if (request.AccuracyMetres > 100)
        {
            isValid = false;
            invalidReason = "GPS accuracy too low (>100m)";
        }
        else if (request.SpeedKmh > 200)
        {
            isValid = false;
            invalidReason = "Speed too high (>200 km/h)";
        }
        else if (request.Latitude < -90 || request.Latitude > 90 || request.Longitude < -180 || request.Longitude > 180)
        {
            isValid = false;
            invalidReason = "Invalid coordinates";
        }

        // Get previous valid ping
        decimal distanceFromPrev = 0;
        decimal cumulative = 0;

        if (isValid)
        {
            var prevPing = await _uow.LocationPings.Query()
                .Where(p => p.SessionId == session.Id && p.IsValid)
                .OrderByDescending(p => p.RecordedAt)
                .FirstOrDefaultAsync();

            if (prevPing != null)
            {
                distanceFromPrev = HaversineKm(prevPing.Latitude, prevPing.Longitude, request.Latitude, request.Longitude);

                // Additional validation: >5km in 30 seconds is suspicious
                if (distanceFromPrev > 5)
                {
                    var timeDiff = (request.RecordedAt ?? DateTime.UtcNow) - prevPing.RecordedAt;
                    if (timeDiff.TotalSeconds < 60)
                    {
                        isValid = false;
                        invalidReason = "Impossible distance jump (>5km in <60s)";
                        distanceFromPrev = 0;
                    }
                }

                cumulative = prevPing.CumulativeDistanceKm + (isValid ? distanceFromPrev : 0);
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
            RecordedAt = request.RecordedAt ?? DateTime.UtcNow,
            IsValid = isValid,
            InvalidReason = invalidReason
        };

        await _uow.LocationPings.AddAsync(ping);

        // Update session totals
        if (isValid)
        {
            session.TotalDistanceKm = cumulative;
            session.AllowanceAmount = cumulative * session.AllowanceRatePerKm;
            await _uow.TrackingSessions.UpdateAsync(session);
        }

        await _uow.SaveChangesAsync();

        // Emit live location via SignalR
        if (isValid && _notifier != null)
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
                    Status = "active"
                };

                await _notifier.SendLiveLocation(payload, user.ZoneId, user.RegionId);
            }
        }

        return new()
        {
            PingId = ping.Id,
            IsValid = isValid,
            CumulativeDistanceKm = cumulative,
            AllowanceAmount = cumulative * session.AllowanceRatePerKm
        };
    }

    // ─── Live Locations ──────────────────────────────────────────────────────

    public async Task<List<LiveLocationDto>> GetLiveLocationsAsync(int userId, string role)
    {
        var todayIst = GetTodayIst();
        var user = await _uow.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new();

        // Get active sessions for today based on role scope
        var sessionsQuery = _uow.TrackingSessions.Query()
            .Include(s => s.User).ThenInclude(u => u!.Zone)
            .Include(s => s.User).ThenInclude(u => u!.Region)
            .Where(s => s.SessionDate == todayIst);

        if (role == "FO")
        {
            sessionsQuery = sessionsQuery.Where(s => s.UserId == userId);
        }
        else if (role == "ZH")
        {
            sessionsQuery = sessionsQuery.Where(s => s.User!.ZoneId == user.ZoneId);
        }
        else if (role == "RH")
        {
            sessionsQuery = sessionsQuery.Where(s => s.User!.RegionId == user.RegionId);
        }
        // SH sees all

        var sessions = await sessionsQuery.ToListAsync();
        var result = new List<LiveLocationDto>();

        foreach (var session in sessions)
        {
            var lastPing = await _uow.LocationPings.Query()
                .Where(p => p.SessionId == session.Id && p.IsValid)
                .OrderByDescending(p => p.RecordedAt)
                .FirstOrDefaultAsync();

            if (lastPing == null && session.Status != TrackingSessionStatus.Active) continue;

            result.Add(new LiveLocationDto
            {
                UserId = session.UserId,
                Name = session.User?.Name ?? "",
                Role = session.Role.ToString(),
                ZoneId = session.User?.ZoneId,
                ZoneName = session.User?.Zone?.Name,
                RegionId = session.User?.RegionId,
                RegionName = session.User?.Region?.Name,
                Latitude = lastPing?.Latitude ?? 0,
                Longitude = lastPing?.Longitude ?? 0,
                SpeedKmh = lastPing?.SpeedKmh,
                LastSeen = lastPing?.RecordedAt ?? session.StartedAt ?? DateTime.UtcNow,
                TotalDistanceKm = session.TotalDistanceKm,
                AllowanceAmount = session.AllowanceAmount,
                Status = StatusToString(session.Status)
            });
        }

        return result;
    }

    // ─── Route ───────────────────────────────────────────────────────────────

    public async Task<RouteResponseDto> GetRouteAsync(int requesterId, string requesterRole, int targetUserId, string date)
    {
        if (!DateTime.TryParse(date, out var parsedDate))
            return new() { Success = false };

        parsedDate = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);

        // Check scope access
        if (requesterRole == "FO" && requesterId != targetUserId)
            return new() { Success = false };

        var targetUser = await _uow.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == targetUserId);

        if (targetUser == null) return new() { Success = false };

        if (requesterRole == "ZH")
        {
            var requester = await _uow.Users.GetByIdAsync(requesterId);
            if (requester?.ZoneId != targetUser.ZoneId)
                return new() { Success = false };
        }
        else if (requesterRole == "RH")
        {
            var requester = await _uow.Users.GetByIdAsync(requesterId);
            if (requester?.RegionId != targetUser.RegionId)
                return new() { Success = false };
        }

        var session = await _uow.TrackingSessions.Query()
            .FirstOrDefaultAsync(s => s.UserId == targetUserId && s.SessionDate == parsedDate);

        if (session == null)
            return new() { Success = false };

        var pings = await _uow.LocationPings.Query()
            .Where(p => p.SessionId == session.Id && p.IsValid)
            .OrderBy(p => p.RecordedAt)
            .Select(p => new RoutePointDto
            {
                Lat = p.Latitude,
                Lon = p.Longitude,
                RecordedAt = p.RecordedAt,
                SpeedKmh = p.SpeedKmh
            })
            .ToListAsync();

        return new()
        {
            User = new RouteUserDto
            {
                Id = targetUser.Id,
                Name = targetUser.Name,
                Role = targetUser.Role.ToString()
            },
            Session = ToDto(session, pings.Count),
            Route = pings
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
            .Where(a => a.AllowanceDate >= fromDate && a.AllowanceDate <= toDate);

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
            Remarks = a.Remarks
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
            Remarks = allowance.Remarks
        };
    }

    // ─── Close Stale Sessions (Midnight Reset) ──────────────────────────────

    public async Task CloseStaleSessionsAsync()
    {
        var todayIst = GetTodayIst();

        var staleSessions = await _uow.TrackingSessions.Query()
            .Where(s => s.Status == TrackingSessionStatus.Active && s.SessionDate < todayIst)
            .ToListAsync();

        foreach (var session in staleSessions)
        {
            var lastPing = await _uow.LocationPings.Query()
                .Where(p => p.SessionId == session.Id && p.IsValid)
                .OrderByDescending(p => p.RecordedAt)
                .FirstOrDefaultAsync();

            var totalDistance = lastPing?.CumulativeDistanceKm ?? 0;
            var allowance = totalDistance * session.AllowanceRatePerKm;

            session.Status = TrackingSessionStatus.Ended;
            session.EndedAt = DateTime.UtcNow;
            session.TotalDistanceKm = totalDistance;
            session.AllowanceAmount = allowance;

            await _uow.TrackingSessions.UpdateAsync(session);

            var existingAllowance = await _uow.DailyAllowances.Query()
                .FirstOrDefaultAsync(a => a.SessionId == session.Id);

            if (existingAllowance == null)
            {
                await _uow.DailyAllowances.AddAsync(new DailyAllowance
                {
                    SessionId = session.Id,
                    UserId = session.UserId,
                    AllowanceDate = session.SessionDate,
                    TotalDistanceKm = totalDistance,
                    RatePerKm = session.AllowanceRatePerKm,
                    GrossAllowance = allowance
                });
            }
        }

        if (staleSessions.Count > 0)
            await _uow.SaveChangesAsync();
    }
}
