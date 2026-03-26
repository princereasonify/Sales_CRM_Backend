namespace SalesCRM.Core.Entities;

public class DeviceLogin : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceUniqueId { get; set; }
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? DeviceOs { get; set; }
    public string? AppVersion { get; set; }
    public string? SimCarrier { get; set; }
    public bool IsEmulator { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
    public bool LoginSuccessful { get; set; } = true;
}
