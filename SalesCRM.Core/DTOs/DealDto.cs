namespace SalesCRM.Core.DTOs;

public class DealDto
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public string School { get; set; } = string.Empty;
    public int FoId { get; set; }
    public string FoName { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalValue { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public List<string> Modules { get; set; } = new();
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public string? ApproverName { get; set; }
    public string? ApprovalNotes { get; set; }
    public int Students { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public int? NumberOfLicenses { get; set; }
    public string? PaymentStatus { get; set; }
    public string? ContractPdfUrl { get; set; }
}

public class CreateDealRequest
{
    public int LeadId { get; set; }
    public decimal ContractValue { get; set; }
    public decimal Discount { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public List<string> Modules { get; set; } = new();
    public string? Notes { get; set; }
    public bool SubmitForApproval { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public int? NumberOfLicenses { get; set; }
    public string? PaymentStatus { get; set; }
}

public class DealApprovalRequest
{
    public bool Approved { get; set; }
    public string? Notes { get; set; }
}
