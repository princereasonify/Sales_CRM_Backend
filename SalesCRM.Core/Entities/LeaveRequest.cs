using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class LeaveRequest : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime LeaveDate { get; set; }
    public LeaveType LeaveType { get; set; }
    public LeaveCategory LeaveCategory { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public string Reason { get; set; } = string.Empty;
    public string? CoverArrangement { get; set; }

    public bool IsSameDay { get; set; }

    public int? ActionedById { get; set; }
    public User? ActionedBy { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? RejectionReason { get; set; }
}
