using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Entities;

public class AiReport : BaseEntity
{
    public int UserId { get; set; }
    public AiReportType ReportType { get; set; }
    public DateTime ReportDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string InputDataJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public int OverallScore { get; set; }
    public string OverallRating { get; set; } = "";
    public AiReportStatus Status { get; set; } = AiReportStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int GeminiTokensUsed { get; set; }
    public DateTime? GeneratedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
