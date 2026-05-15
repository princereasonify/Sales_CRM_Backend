using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? Avatar { get; set; }

    public int? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public int? RegionId { get; set; }
    public Region? Region { get; set; }

    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    // Soft-delete flag — separate from IsActive (which is also used for the
    // signup-approval gate). Deleted users keep their FK references intact
    // but are hidden from every user-facing list and blocked from login.
    public bool IsDeleted { get; set; } = false;

    public decimal? HomeLatitude { get; set; }
    public decimal? HomeLongitude { get; set; }
    public string? HomeAddress { get; set; }

    public string? FcmToken { get; set; }

    public decimal TravelAllowanceRate { get; set; } = 10.00m;

    public ICollection<Lead> Leads { get; set; } = new List<Lead>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
