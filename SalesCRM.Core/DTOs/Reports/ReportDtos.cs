namespace SalesCRM.Core.DTOs.Reports;

public class UserPerformanceDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int TotalVisits { get; set; }
    public int TotalDemos { get; set; }
    public int TotalDeals { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalDistanceKm { get; set; }
    public decimal TotalAllowance { get; set; }
    public decimal AvgVisitDurationMinutes { get; set; }
    public int SchoolsVisited { get; set; }
    public decimal ConversionRate { get; set; }
}

public class SchoolVisitSummaryDto
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string? City { get; set; }
    public int TotalVisits { get; set; }
    public decimal TotalTimeSpentMinutes { get; set; }
    public decimal AvgVisitDurationMinutes { get; set; }
    public int UniqueVisitors { get; set; }
    public DateTime? LastVisitDate { get; set; }
    public string? LastOutcome { get; set; }
}

public class TeamComparisonDto
{
    public string TeamName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int TotalVisits { get; set; }
    public int TotalDemos { get; set; }
    public int TotalDeals { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgVisitsPerMember { get; set; }
    public decimal ConversionRate { get; set; }
}

public class PipelineReportDto
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AvgAgeDays { get; set; }
}

public class ReportFilters
{
    public int? UserId { get; set; }
    public string? Role { get; set; }
    public int? ZoneId { get; set; }
    public int? RegionId { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public string? LeadStage { get; set; }
    public string? ActivityType { get; set; }
}
