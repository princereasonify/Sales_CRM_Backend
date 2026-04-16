namespace SalesCRM.Core.DTOs.Leave;

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public DateTime LeaveDate { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public string LeaveCategory { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? CoverArrangement { get; set; }
    public bool IsSameDay { get; set; }
    public int? ActionedById { get; set; }
    public string? ActionedByName { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? PlanImpactMessage { get; set; }
}

public class ApplyLeaveRequest
{
    public DateTime LeaveDate { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public string LeaveCategory { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? CoverArrangement { get; set; }
}

public class RejectLeaveRequest
{
    public string RejectionReason { get; set; } = string.Empty;
}
