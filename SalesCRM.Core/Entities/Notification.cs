using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class Notification : BaseEntity
{
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
