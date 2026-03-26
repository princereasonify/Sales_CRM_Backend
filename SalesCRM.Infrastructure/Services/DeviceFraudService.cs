using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.DeviceFraud;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class DeviceFraudService : IDeviceFraudService
{
    private readonly IUnitOfWork _uow;

    public DeviceFraudService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task ProcessLoginAsync(int userId, DeviceInfoDto? deviceInfo, string? ipAddress, string? userAgent)
    {
        var fingerprint = GenerateFingerprint(deviceInfo);

        // 1. Record the login
        var login = new DeviceLogin
        {
            UserId = userId,
            DeviceFingerprint = fingerprint,
            DeviceUniqueId = deviceInfo?.DeviceUniqueId,
            DeviceBrand = deviceInfo?.DeviceBrand,
            DeviceModel = deviceInfo?.DeviceModel,
            DeviceOs = deviceInfo?.DeviceOs,
            AppVersion = deviceInfo?.AppVersion,
            SimCarrier = deviceInfo?.SimCarrier,
            IsEmulator = deviceInfo?.IsEmulator ?? false,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            LoginAt = DateTime.UtcNow,
            LoginSuccessful = true
        };
        await _uow.DeviceLogins.AddAsync(login);

        // 2. Upsert UserDevice
        var existingDevice = await _uow.UserDevices.Query()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceFingerprint == fingerprint);

        bool isNewDevice = existingDevice == null;

        if (existingDevice != null)
        {
            existingDevice.LastSeenAt = DateTime.UtcNow;
            existingDevice.LoginCount++;
            existingDevice.DeviceOs = deviceInfo?.DeviceOs ?? existingDevice.DeviceOs;

            // Auto-promote trust level
            if (existingDevice.LoginCount >= 10 && existingDevice.TrustLevel != DeviceTrustLevel.Trusted)
                existingDevice.TrustLevel = DeviceTrustLevel.Trusted;
            else if (existingDevice.LoginCount >= 3 && existingDevice.TrustLevel == DeviceTrustLevel.New)
                existingDevice.TrustLevel = DeviceTrustLevel.Known;

            await _uow.UserDevices.UpdateAsync(existingDevice);
        }
        else
        {
            var isFirst = !await _uow.UserDevices.Query().AnyAsync(d => d.UserId == userId);
            var newDevice = new UserDevice
            {
                UserId = userId,
                DeviceFingerprint = fingerprint,
                DeviceUniqueId = deviceInfo?.DeviceUniqueId,
                DeviceBrand = deviceInfo?.DeviceBrand,
                DeviceModel = deviceInfo?.DeviceModel,
                DeviceOs = deviceInfo?.DeviceOs,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                LoginCount = 1,
                IsPrimary = isFirst,
                TrustLevel = DeviceTrustLevel.New
            };
            await _uow.UserDevices.AddAsync(newDevice);
        }

        await _uow.SaveChangesAsync();

        // 3. Run fraud detection rules (only if we have a real fingerprint)
        if (fingerprint != "unknown")
        {
            await CheckSameDeviceMultipleAccounts(userId, fingerprint, deviceInfo);
            await CheckRapidDeviceSwitch(userId, fingerprint, deviceInfo);
            if (isNewDevice)
                await CheckNewDevice(userId, fingerprint, deviceInfo);
        }
    }

    private async Task CheckSameDeviceMultipleAccounts(int userId, string fingerprint, DeviceInfoDto? deviceInfo)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var otherLogins = await _uow.DeviceLogins.Query()
            .Where(d => d.DeviceFingerprint == fingerprint && d.UserId != userId && d.LoginAt >= cutoff && d.LoginSuccessful)
            .Select(d => new { d.UserId, d.LoginAt, d.IpAddress })
            .Distinct()
            .ToListAsync();

        if (!otherLogins.Any()) return;

        foreach (var other in otherLogins.DistinctBy(o => o.UserId))
        {
            // Check if alert already exists for this pair today
            var today = DateTime.UtcNow.Date;
            var exists = await _uow.DeviceFraudAlerts.Query()
                .AnyAsync(a => a.FraudType == DeviceFraudType.SameDeviceMultipleAccounts
                    && a.DeviceFingerprint == fingerprint
                    && ((a.UserId == userId && a.OtherUserId == other.UserId) || (a.UserId == other.UserId && a.OtherUserId == userId))
                    && a.DetectedAt >= today);

            if (exists) continue;

            var user = await _uow.Users.GetByIdAsync(userId);
            var otherUser = await _uow.Users.GetByIdAsync(other.UserId);
            var deviceName = $"{deviceInfo?.DeviceBrand} {deviceInfo?.DeviceModel}".Trim();
            if (string.IsNullOrWhiteSpace(deviceName)) deviceName = "Unknown Device";

            var alert = new DeviceFraudAlert
            {
                FraudType = DeviceFraudType.SameDeviceMultipleAccounts,
                Severity = AlertSeverity.High,
                UserId = userId,
                OtherUserId = other.UserId,
                DeviceFingerprint = fingerprint,
                Title = $"Same device used by {user?.Name} and {otherUser?.Name}",
                Description = $"{deviceName} was used to login as {user?.Name} and {otherUser?.Name} within 24 hours. This indicates potential credential sharing.",
                EvidenceJson = JsonSerializer.Serialize(new
                {
                    device = new { brand = deviceInfo?.DeviceBrand, model = deviceInfo?.DeviceModel, os = deviceInfo?.DeviceOs },
                    user1 = new { id = userId, name = user?.Name, loginAt = DateTime.UtcNow },
                    user2 = new { id = other.UserId, name = otherUser?.Name, loginAt = other.LoginAt },
                    sameIp = other.IpAddress != null
                }),
                DetectedAt = DateTime.UtcNow
            };
            await _uow.DeviceFraudAlerts.AddAsync(alert);
            await _uow.SaveChangesAsync();
        }
    }

    private async Task CheckRapidDeviceSwitch(int userId, string fingerprint, DeviceInfoDto? deviceInfo)
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var previousLogin = await _uow.DeviceLogins.Query()
            .Where(d => d.UserId == userId && d.LoginSuccessful && d.LoginAt >= cutoff && d.DeviceFingerprint != fingerprint)
            .OrderByDescending(d => d.LoginAt)
            .FirstOrDefaultAsync();

        if (previousLogin == null) return;

        // Check if alert already exists today
        var today = DateTime.UtcNow.Date;
        var exists = await _uow.DeviceFraudAlerts.Query()
            .AnyAsync(a => a.FraudType == DeviceFraudType.RapidDeviceSwitch
                && a.UserId == userId
                && a.DetectedAt >= today);

        if (exists) return;

        var user = await _uow.Users.GetByIdAsync(userId);
        var timeDiff = (DateTime.UtcNow - previousLogin.LoginAt).TotalMinutes;

        var alert = new DeviceFraudAlert
        {
            FraudType = DeviceFraudType.RapidDeviceSwitch,
            Severity = AlertSeverity.Medium,
            UserId = userId,
            DeviceFingerprint = fingerprint,
            Title = $"{user?.Name} switched devices within {(int)timeDiff} minutes",
            Description = $"{user?.Name} logged in from {previousLogin.DeviceBrand} {previousLogin.DeviceModel} at {previousLogin.LoginAt:HH:mm} and then from {deviceInfo?.DeviceBrand} {deviceInfo?.DeviceModel} at {DateTime.UtcNow:HH:mm} UTC.",
            EvidenceJson = JsonSerializer.Serialize(new
            {
                previousDevice = new { brand = previousLogin.DeviceBrand, model = previousLogin.DeviceModel, os = previousLogin.DeviceOs, loginAt = previousLogin.LoginAt },
                currentDevice = new { brand = deviceInfo?.DeviceBrand, model = deviceInfo?.DeviceModel, os = deviceInfo?.DeviceOs, loginAt = DateTime.UtcNow },
                timeDiffMinutes = (int)timeDiff
            }),
            DetectedAt = DateTime.UtcNow
        };
        await _uow.DeviceFraudAlerts.AddAsync(alert);
        await _uow.SaveChangesAsync();
    }

    private async Task CheckNewDevice(int userId, string fingerprint, DeviceInfoDto? deviceInfo)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        var deviceName = $"{deviceInfo?.DeviceBrand} {deviceInfo?.DeviceModel}".Trim();
        if (string.IsNullOrWhiteSpace(deviceName)) deviceName = "Unknown Device";

        var alert = new DeviceFraudAlert
        {
            FraudType = DeviceFraudType.NewDevice,
            Severity = AlertSeverity.Low,
            UserId = userId,
            DeviceFingerprint = fingerprint,
            Title = $"{user?.Name} logged in from a new device: {deviceName}",
            Description = $"First-time login from {deviceName} ({deviceInfo?.DeviceOs}) for user {user?.Name}.",
            EvidenceJson = JsonSerializer.Serialize(new
            {
                device = new { brand = deviceInfo?.DeviceBrand, model = deviceInfo?.DeviceModel, os = deviceInfo?.DeviceOs, uniqueId = deviceInfo?.DeviceUniqueId },
                isEmulator = deviceInfo?.IsEmulator ?? false,
                simCarrier = deviceInfo?.SimCarrier
            }),
            DetectedAt = DateTime.UtcNow
        };
        await _uow.DeviceFraudAlerts.AddAsync(alert);
        await _uow.SaveChangesAsync();
    }

    private static string GenerateFingerprint(DeviceInfoDto? info)
    {
        if (info == null) return "web-unknown";
        var raw = $"{info.DeviceUniqueId ?? ""}|{info.DeviceBrand ?? ""}|{info.DeviceModel ?? ""}";
        if (raw == "||") return "unknown";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // --- Query Methods ---

    public async Task<DeviceFraudSummaryDto> GetFraudSummaryAsync(int requesterId, string role)
    {
        var query = GetScopedAlertQuery(requesterId, role);

        var alerts = await query.OrderByDescending(a => a.DetectedAt).ToListAsync();

        return new DeviceFraudSummaryDto
        {
            TotalAlerts = alerts.Count,
            NewAlerts = alerts.Count(a => a.Status == AlertStatus.New),
            HighSeverityAlerts = alerts.Count(a => a.Severity == AlertSeverity.High || a.Severity == AlertSeverity.Critical),
            CredentialSharingAlerts = alerts.Count(a => a.FraudType == DeviceFraudType.SameDeviceMultipleAccounts),
            DeviceSwitchAlerts = alerts.Count(a => a.FraudType == DeviceFraudType.RapidDeviceSwitch),
            RecentAlerts = alerts.Take(10).Select(MapToAlertDto).ToList()
        };
    }

    public async Task<List<DeviceFraudAlertDto>> GetAlertsAsync(int requesterId, string role, string? fraudType = null, string? severity = null, string? status = null)
    {
        var query = GetScopedAlertQuery(requesterId, role);

        if (!string.IsNullOrEmpty(fraudType) && Enum.TryParse<DeviceFraudType>(fraudType, out var ft))
            query = query.Where(a => a.FraudType == ft);
        if (!string.IsNullOrEmpty(severity) && Enum.TryParse<AlertSeverity>(severity, out var sv))
            query = query.Where(a => a.Severity == sv);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AlertStatus>(status, out var st))
            query = query.Where(a => a.Status == st);

        var alerts = await query.OrderByDescending(a => a.DetectedAt).Take(100).ToListAsync();
        return alerts.Select(MapToAlertDto).ToList();
    }

    public async Task<DeviceFraudAlertDetailDto?> GetAlertDetailAsync(int alertId, int requesterId, string role)
    {
        var alert = await GetScopedAlertQuery(requesterId, role)
            .FirstOrDefaultAsync(a => a.Id == alertId);

        if (alert == null) return null;

        var recentLogins = await _uow.DeviceLogins.Query()
            .Where(d => d.DeviceFingerprint == alert.DeviceFingerprint && d.LoginAt >= DateTime.UtcNow.AddDays(-7))
            .OrderByDescending(d => d.LoginAt)
            .Take(20)
            .ToListAsync();

        var userIds = recentLogins.Select(l => l.UserId).Distinct().ToList();
        var users = await _uow.Users.Query().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);

        var dto = new DeviceFraudAlertDetailDto
        {
            Id = alert.Id,
            FraudType = alert.FraudType.ToString(),
            Severity = alert.Severity.ToString(),
            Status = alert.Status.ToString(),
            UserId = alert.UserId,
            UserName = alert.User?.Name ?? "",
            UserRole = alert.User?.Role.ToString() ?? "",
            OtherUserId = alert.OtherUserId,
            OtherUserName = alert.OtherUser?.Name,
            DeviceFingerprint = alert.DeviceFingerprint,
            Title = alert.Title,
            Description = alert.Description,
            EvidenceJson = alert.EvidenceJson,
            DetectedAt = alert.DetectedAt,
            ReviewedByName = alert.ReviewedBy?.Name,
            ReviewedAt = alert.ReviewedAt,
            ReviewNotes = alert.ReviewNotes,
            RecentLogins = recentLogins.Select(l => new DeviceLoginSummaryDto
            {
                Id = l.Id,
                UserId = l.UserId,
                UserName = users.GetValueOrDefault(l.UserId, ""),
                DeviceFingerprint = l.DeviceFingerprint,
                DeviceBrand = l.DeviceBrand,
                DeviceModel = l.DeviceModel,
                DeviceOs = l.DeviceOs,
                AppVersion = l.AppVersion,
                SimCarrier = l.SimCarrier,
                IsEmulator = l.IsEmulator,
                IpAddress = l.IpAddress,
                LoginAt = l.LoginAt
            }).ToList()
        };

        return dto;
    }

    public async Task<DeviceFraudAlertDto?> ReviewAlertAsync(int alertId, int reviewerId, ReviewAlertRequest request)
    {
        var alert = await _uow.DeviceFraudAlerts.Query()
            .Include(a => a.User)
            .Include(a => a.OtherUser)
            .FirstOrDefaultAsync(a => a.Id == alertId);

        if (alert == null) return null;

        if (Enum.TryParse<AlertStatus>(request.Status, out var newStatus))
            alert.Status = newStatus;

        alert.ReviewedById = reviewerId;
        alert.ReviewedAt = DateTime.UtcNow;
        alert.ReviewNotes = request.ReviewNotes;

        await _uow.DeviceFraudAlerts.UpdateAsync(alert);
        await _uow.SaveChangesAsync();

        return MapToAlertDto(alert);
    }

    public async Task<List<UserDeviceDto>> GetUserDevicesAsync(int userId)
    {
        var devices = await _uow.UserDevices.Query()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync();

        return devices.Select(d => new UserDeviceDto
        {
            Id = d.Id,
            DeviceFingerprint = d.DeviceFingerprint,
            DeviceBrand = d.DeviceBrand,
            DeviceModel = d.DeviceModel,
            DeviceOs = d.DeviceOs,
            FirstSeenAt = d.FirstSeenAt,
            LastSeenAt = d.LastSeenAt,
            LoginCount = d.LoginCount,
            IsPrimary = d.IsPrimary,
            TrustLevel = d.TrustLevel.ToString()
        }).ToList();
    }

    public async Task<List<DeviceLoginSummaryDto>> GetLoginHistoryAsync(int userId, int count = 20)
    {
        var logins = await _uow.DeviceLogins.Query()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LoginAt)
            .Take(count)
            .ToListAsync();

        var user = await _uow.Users.GetByIdAsync(userId);

        return logins.Select(l => new DeviceLoginSummaryDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserName = user?.Name ?? "",
            DeviceFingerprint = l.DeviceFingerprint,
            DeviceBrand = l.DeviceBrand,
            DeviceModel = l.DeviceModel,
            DeviceOs = l.DeviceOs,
            AppVersion = l.AppVersion,
            SimCarrier = l.SimCarrier,
            IsEmulator = l.IsEmulator,
            IpAddress = l.IpAddress,
            LoginAt = l.LoginAt
        }).ToList();
    }

    // --- Helpers ---

    private IQueryable<DeviceFraudAlert> GetScopedAlertQuery(int requesterId, string role)
    {
        var query = _uow.DeviceFraudAlerts.Query()
            .Include(a => a.User)
            .Include(a => a.OtherUser)
            .Include(a => a.ReviewedBy)
            .AsQueryable();

        if (role == "ZH")
        {
            var requesterZoneId = _uow.Users.Query().Where(u => u.Id == requesterId).Select(u => u.ZoneId).FirstOrDefault();
            var zoneUserIds = _uow.Users.Query().Where(u => u.ZoneId == requesterZoneId).Select(u => u.Id).ToList();
            query = query.Where(a => zoneUserIds.Contains(a.UserId));
        }
        else if (role == "RH")
        {
            var requesterRegionId = _uow.Users.Query().Where(u => u.Id == requesterId).Select(u => u.RegionId).FirstOrDefault();
            var regionZoneIds = _uow.Zones.Query().Where(z => z.RegionId == requesterRegionId).Select(z => z.Id).ToList();
            var regionUserIds = _uow.Users.Query().Where(u => regionZoneIds.Contains(u.ZoneId ?? 0)).Select(u => u.Id).ToList();
            query = query.Where(a => regionUserIds.Contains(a.UserId));
        }
        // SH and SCA see all

        return query;
    }

    private static DeviceFraudAlertDto MapToAlertDto(DeviceFraudAlert a) => new()
    {
        Id = a.Id,
        FraudType = a.FraudType.ToString(),
        Severity = a.Severity.ToString(),
        Status = a.Status.ToString(),
        UserId = a.UserId,
        UserName = a.User?.Name ?? "",
        UserRole = a.User?.Role.ToString() ?? "",
        OtherUserId = a.OtherUserId,
        OtherUserName = a.OtherUser?.Name,
        DeviceFingerprint = a.DeviceFingerprint,
        Title = a.Title,
        Description = a.Description,
        DetectedAt = a.DetectedAt,
        ReviewedByName = a.ReviewedBy?.Name,
        ReviewedAt = a.ReviewedAt,
        ReviewNotes = a.ReviewNotes
    };
}
