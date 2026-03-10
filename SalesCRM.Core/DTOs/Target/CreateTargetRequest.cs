namespace SalesCRM.Core.DTOs.Target;

public class CreateTargetRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public int NumberOfSchools { get; set; }
    public string PeriodType { get; set; } = "Quarterly";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int AssignedToId { get; set; }
    public int? ParentTargetId { get; set; }
}
