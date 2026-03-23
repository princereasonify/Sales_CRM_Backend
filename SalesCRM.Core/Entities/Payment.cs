using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class Payment : BaseEntity
{
    public int DealId { get; set; }
    public int? SchoolId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? TransactionId { get; set; }
    public string? GatewayProvider { get; set; }
    public string? ChequeNumber { get; set; }
    public string? ChequeImageUrl { get; set; }
    public string? BankName { get; set; }
    public string? UpiId { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? Notes { get; set; }
    public int CollectedById { get; set; }
    public int? VerifiedById { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public Deal Deal { get; set; } = null!;
    public School? School { get; set; }
    public User CollectedBy { get; set; } = null!;
    public User? VerifiedBy { get; set; }
}
