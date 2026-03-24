namespace SalesCRM.Core.DTOs;

public class AiReportListDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string UserRole { get; set; } = "";
    public string ReportType { get; set; } = "";
    public DateTime ReportDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int OverallScore { get; set; }
    public string OverallRating { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? GeneratedAt { get; set; }
}

public class AiReportDetailDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string UserRole { get; set; } = "";
    public string ReportType { get; set; } = "";
    public DateTime ReportDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string OutputJson { get; set; } = "{}";
    public int OverallScore { get; set; }
    public string OverallRating { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? GeneratedAt { get; set; }
}

public class AiReportFilterDto
{
    public string? ReportType { get; set; }
    public int? UserId { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
