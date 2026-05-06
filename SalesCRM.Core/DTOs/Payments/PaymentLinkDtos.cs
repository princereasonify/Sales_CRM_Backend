namespace SalesCRM.Core.DTOs.Payments;

public class PaymentLinkDto
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string? JuspayOrderRef { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string? Description { get; set; }
    public DateTime DueDate { get; set; }
    public string? PaymentUrl { get; set; }
    public DateTime? ExpiryAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public int CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreatePaymentLinkRequest
{
    public int SchoolId { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public string? Description { get; set; }
}

public class EligibleSchoolDto
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactName { get; set; }
}
