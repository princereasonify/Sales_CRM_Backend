using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class DeviceFraudAlert : BaseEntity
{
    public DeviceFraudType FraudType { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.New;

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? OtherUserId { get; set; }
    public User? OtherUser { get; set; }

    public string DeviceFingerprint { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? EvidenceJson { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public int? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}
