using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class ExpenseClaim : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime ExpenseDate { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? BillUrl { get; set; }

    public ExpenseClaimStatus Status { get; set; } = ExpenseClaimStatus.Pending;

    public int? ActionedById { get; set; }
    public User? ActionedBy { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? RejectionReason { get; set; }
}
