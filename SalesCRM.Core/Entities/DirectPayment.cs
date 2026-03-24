using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class DirectPayment : BaseEntity
{
    public int RecipientId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? TransactionId { get; set; }
    public string? UpiId { get; set; }
    public string? BankName { get; set; }
    public string? Notes { get; set; }
    public string Purpose { get; set; } = string.Empty; // Bonus, Allowance, Incentive, Reimbursement
    public int PaidById { get; set; }

    public User Recipient { get; set; } = null!;
    public User PaidBy { get; set; } = null!;
}
