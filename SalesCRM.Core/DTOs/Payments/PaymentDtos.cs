namespace SalesCRM.Core.DTOs.Payments;

public class PaymentDto
{
    public int Id { get; set; }
    public int DealId { get; set; }
    public string? DealName { get; set; }
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? ChequeNumber { get; set; }
    public string? ChequeImageUrl { get; set; }
    public string? BankName { get; set; }
    public string? UpiId { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? Notes { get; set; }
    public string? CollectedByName { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePaymentRequest
{
    public int DealId { get; set; }
    public int? SchoolId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";
    public string? TransactionId { get; set; }
    public string? ChequeNumber { get; set; }
    public string? ChequeImageUrl { get; set; }
    public string? BankName { get; set; }
    public string? UpiId { get; set; }
    public string? Notes { get; set; }
}

public class VerifyPaymentRequest
{
    public bool Verified { get; set; }
    public string? Notes { get; set; }
}
