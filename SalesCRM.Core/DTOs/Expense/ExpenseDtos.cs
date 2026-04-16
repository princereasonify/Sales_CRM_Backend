namespace SalesCRM.Core.DTOs.Expense;

public class ExpenseClaimDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? BillUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ActionedById { get; set; }
    public string? ActionedByName { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExpenseClaimRequest
{
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public class RejectExpenseClaimRequest
{
    public string RejectionReason { get; set; } = string.Empty;
}

public class BulkApproveExpenseRequest
{
    public List<int> Ids { get; set; } = new();
}
