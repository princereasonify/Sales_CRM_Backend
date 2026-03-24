namespace SalesCRM.Core.DTOs.Payments;

public class DirectPaymentDto
{
    public int Id { get; set; }
    public int RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientRole { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? UpiId { get; set; }
    public string? BankName { get; set; }
    public string? Notes { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string PaidByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateDirectPaymentRequest
{
    public int RecipientId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "BankTransfer";
    public string? TransactionId { get; set; }
    public string? UpiId { get; set; }
    public string? BankName { get; set; }
    public string? Notes { get; set; }
    public string Purpose { get; set; } = "Bonus";
}
