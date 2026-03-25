namespace SalesCRM.Core.DTOs.VisitReports;

public class VisitReportDto
{
    public int Id { get; set; }
    public int? SchoolVisitLogId { get; set; }
    public int? ActivityId { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int? PersonMetId { get; set; }
    public string? PersonMetName { get; set; }
    public string? Outcome { get; set; }
    public string? Remarks { get; set; }
    public string NextAction { get; set; } = "None";
    public DateTime? NextActionDate { get; set; }
    public string? NextActionNotes { get; set; }
    public string? CustomFields { get; set; }
    public string? Photos { get; set; }
    public string? Videos { get; set; }
    public string? AudioNotes { get; set; }
    public string? FeedbackSentiment { get; set; }
    public string? FeedbackText { get; set; }
    public string? FeedbackPersonName { get; set; }
    public string? FeedbackPersonDesignation { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateVisitReportRequest
{
    public int? SchoolVisitLogId { get; set; }
    public int? ActivityId { get; set; }
    public int? SchoolId { get; set; }
    public string Purpose { get; set; } = "Visit";
    public int? PersonMetId { get; set; }
    public string? Outcome { get; set; }
    public string? Remarks { get; set; }
    public string NextAction { get; set; } = "None";
    public DateTime? NextActionDate { get; set; }
    public string? NextActionNotes { get; set; }
    public string? CustomFields { get; set; }
    public string? Photos { get; set; }
    public string? Videos { get; set; }
    public string? AudioNotes { get; set; }
    public string? FeedbackSentiment { get; set; }
    public string? FeedbackText { get; set; }
    public string? FeedbackPersonName { get; set; }
    public string? FeedbackPersonDesignation { get; set; }
}

public class VisitFieldConfigDto
{
    public int Id { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public string? Options { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateVisitFieldConfigRequest
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Dropdown";
    public string? Options { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
}
