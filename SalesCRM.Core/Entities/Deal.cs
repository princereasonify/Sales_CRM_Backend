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
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovalNotes { get; set; }

    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;

    public int FoId { get; set; }
    public User Fo { get; set; } = null!;

    public int? ApproverId { get; set; }
    public User? Approver { get; set; }
}
