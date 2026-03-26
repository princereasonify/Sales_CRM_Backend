namespace SalesCRM.Core.DTOs.DeviceFraud;

public class DeviceInfoDto
{
    public string? DeviceUniqueId { get; set; }
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? DeviceOs { get; set; }
    public string? AppVersion { get; set; }
    public string? SimCarrier { get; set; }
    public bool IsEmulator { get; set; }
}

public class DeviceFraudAlertDto
{
    public int Id { get; set; }
    public string FraudType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public int? OtherUserId { get; set; }
    public string? OtherUserName { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}

public class DeviceFraudAlertDetailDto : DeviceFraudAlertDto
{
    public string? EvidenceJson { get; set; }
    public List<DeviceLoginSummaryDto> RecentLogins { get; set; } = new();
}

public class DeviceLoginSummaryDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? DeviceOs { get; set; }
    public string? AppVersion { get; set; }
    public string? SimCarrier { get; set; }
    public bool IsEmulator { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LoginAt { get; set; }
}

public class UserDeviceDto
{
    public int Id { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? DeviceOs { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public int LoginCount { get; set; }
    public bool IsPrimary { get; set; }
    public string TrustLevel { get; set; } = string.Empty;
}

public class ReviewAlertRequest
{
    public string Status { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
}

public class DeviceFraudSummaryDto
{
    public int TotalAlerts { get; set; }
    public int NewAlerts { get; set; }
    public int HighSeverityAlerts { get; set; }
    public int CredentialSharingAlerts { get; set; }
    public int DeviceSwitchAlerts { get; set; }
    public List<DeviceFraudAlertDto> RecentAlerts { get; set; } = new();
}
