using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class Deal : BaseEntity
{
    public decimal ContractValue { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalValue { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Modules { get; set; } = string.Empty; // JSON array stored as string

    // Pricing & GST fields
    public decimal BasePrice { get; set; }
    public int TotalLogins { get; set; }
    public decimal Subtotal { get; set; }
    public decimal AmountWithoutGst { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalMoney { get; set; }
    public string? BillingFrequency { get; set; }
    public DateTime? OnboardingDate { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovalNotes { get; set; }

    // Contract management fields
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public int? NumberOfLicenses { get; set; }
    public string? PaymentStatus { get; set; } // Pending, Partial, Paid
    public string? ContractPdfUrl { get; set; }

    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;

    public int FoId { get; set; }
    public User Fo { get; set; } = null!;

    public int? ApproverId { get; set; }
    public User? Approver { get; set; }
}
