using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class UserDevice : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceUniqueId { get; set; }
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? DeviceOs { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public int LoginCount { get; set; } = 1;

    public bool IsPrimary { get; set; }
    public DeviceTrustLevel TrustLevel { get; set; } = DeviceTrustLevel.New;
}
