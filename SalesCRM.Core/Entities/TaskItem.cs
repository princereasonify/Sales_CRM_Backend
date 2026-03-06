using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class TaskItem : BaseEntity
{
    public DateTime ScheduledTime { get; set; }
    public ActivityType Type { get; set; }
    public string School { get; set; } = string.Empty;
    public bool IsDone { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
}
