namespace SalesCRM.Core.Entities;

public class PaymentLink : BaseEntity
{
    public int SchoolId { get; set; }
    public School School { get; set; } = null!;

    public string OrderId { get; set; } = string.Empty;
    public string? JuspayOrderRef { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string? Description { get; set; }
    public DateTime DueDate { get; set; }

    public string? PaymentUrl { get; set; }
    public DateTime? ExpiryAt { get; set; }

    /// pending | sent | paid | failed
    public string Status { get; set; } = "pending";
    public DateTime? PaidAt { get; set; }

    public string? LastWebhookPayload { get; set; }

    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
}
