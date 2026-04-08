namespace SalesCRM.Core.DTOs;

public class FoDashboardDto
{
    public decimal Revenue { get; set; }
    public decimal RevenueTarget { get; set; }
    public int VisitsThisWeek { get; set; }
    public int DemosThisMonth { get; set; }
    public int DealsWon { get; set; }
    public int DealsLost { get; set; }
    public int PipelineLeads { get; set; }
    public decimal PipelineValue { get; set; }
    public List<LeadListDto> HotLeads { get; set; } = new();
    public List<TaskItemDto> TodaysTasks { get; set; } = new();
    public List<ActivityDto> RecentActivities { get; set; } = new();

    // Time breakdown (today's session)
    public decimal HoursWorked { get; set; }
    public decimal TotalDistanceKm { get; set; }
    public decimal AllowanceAmount { get; set; }
    public decimal InSchoolMinutes { get; set; }
    public decimal TravellingMinutes { get; set; }
    public decimal IdleMinutes { get; set; }

    // Activity counts (this month)
    public int VisitsThisMonth { get; set; }
    public int FollowUpsThisMonth { get; set; }

    // Activity targets (from backend — frontend must NOT hardcode these)
    public int VisitsTargetWeekly { get; set; }
    public int VisitsTargetMonthly { get; set; }
    public int DemosTargetMonthly { get; set; }
    public int FollowUpsTargetMonthly { get; set; }
    public int DealsTargetMonthly { get; set; }
    public decimal AllowanceRatePerKm { get; set; }

    // Conversion funnel
    public List<FunnelStage> ConversionFunnel { get; set; } = new();

    // Deal aging
    public List<AgingDeal> AgingDeals { get; set; } = new();
}

public class FunnelStage
{
    public string Stage { get; set; } = "";
    public int Count { get; set; }
    public decimal Value { get; set; }
}

public class AgingDeal
{
    public string School { get; set; } = "";
    public decimal Value { get; set; }
    public string Stage { get; set; } = "";
    public int DaysInStage { get; set; }
    public string Risk { get; set; } = "LOW";
}

public class TaskItemDto
{
    public int Id { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string Type { get; set; } = string.Empty;
    public string School { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int? LeadId { get; set; }
}

public class ZoneDashboardDto
{
    public string ZoneName { get; set; } = string.Empty;
    public decimal RevenueMTD { get; set; }
    public decimal RevenueTarget { get; set; }
    public int TargetPct { get; set; }
    public int ActivePipeline { get; set; }
    public decimal PipelineValue { get; set; }
    public int PendingApprovals { get; set; }
    public int WinRate { get; set; }
    public int AtRiskFOs { get; set; }
    public int TotalFOs { get; set; }
    public int DealsLost { get; set; }

    // Activity counts (this month)
    public int VisitsThisMonth { get; set; }
    public int DemosThisMonth { get; set; }
    public int CallsThisMonth { get; set; }

    // Zone-level targets (computed: per-FO target × totalFOs)
    public int VisitsTargetMonthly { get; set; }
    public int DemosTargetMonthly { get; set; }
    public int CallsTargetMonthly { get; set; }

    public List<FoPerformanceDto> FoPerformance { get; set; } = new();
    public List<DealDto> PendingDeals { get; set; } = new();
    public List<FunnelStage> ConversionFunnel { get; set; } = new();
    public List<AgingDeal> AgingDeals { get; set; } = new();
    public List<ChartDataPoint> RevenueChart { get; set; } = new();
}

public class FoPerformanceDto
{
    public int FoId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Territory { get; set; }
    public string? Zone { get; set; }
    public string? Region { get; set; }
    public decimal Revenue { get; set; }
    public decimal Target { get; set; }
    public int TargetPct { get; set; }
    public int VisitsWeek { get; set; }
    public int DemosMonth { get; set; }
    public int DealsWon { get; set; }
    public int PipelineLeads { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RegionDashboardDto
{
    public string RegionName { get; set; } = string.Empty;
    public decimal RevenueMTD { get; set; }
    public decimal RevenueTarget { get; set; }
    public int TargetPct { get; set; }
    public int ActiveLeads { get; set; }
    public decimal PipelineValue { get; set; }
    public int DealsWon { get; set; }
    public int DealsLost { get; set; }
    public int WinRate { get; set; }
    public int ForecastAccuracy { get; set; }
    public int TotalFOs { get; set; }
    public int TotalZones { get; set; }
    public int PendingApprovals { get; set; }

    // Activity counts (this month)
    public int VisitsThisMonth { get; set; }
    public int DemosThisMonth { get; set; }

    public List<ZoneSummaryDto> Zones { get; set; } = new();
    public List<ChartDataPoint> RevenueChart { get; set; } = new();
    public List<FunnelStage> ConversionFunnel { get; set; } = new();
    public List<AgingDeal> AgingDeals { get; set; } = new();
    public List<LossReasonDto> LossReasons { get; set; } = new();
}

public class ZoneSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Target { get; set; }
    public int TargetPct { get; set; }
    public int WinRate { get; set; }
    public int Pipeline { get; set; }
    public int FoCount { get; set; }
    public int DealsWon { get; set; }
    public string Health { get; set; } = string.Empty;
}

public class NationalDashboardDto
{
    public decimal RevenueMTD { get; set; }
    public decimal RevenueTarget { get; set; }
    public int TargetPct { get; set; }
    public int SchoolsWon { get; set; }
    public int DealsLost { get; set; }
    public decimal PipelineValue { get; set; }
    public int WinRate { get; set; }
    public int ActiveLeads { get; set; }
    public int TotalFOs { get; set; }
    public int TotalZones { get; set; }
    public int TotalRegions { get; set; }
    public int PendingApprovals { get; set; }

    // Activity counts (this month)
    public int VisitsThisMonth { get; set; }
    public int DemosThisMonth { get; set; }

    public List<RegionSummaryDto> Regions { get; set; } = new();
    public List<ChartDataPoint> RevenueChart { get; set; } = new();
    public List<LossReasonDto> LossReasons { get; set; } = new();
    public List<FunnelStage> ConversionFunnel { get; set; } = new();
    public List<AgingDeal> AgingDeals { get; set; } = new();
    public List<FoPerformanceDto> TopPerformers { get; set; } = new();
}

public class RegionSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Target { get; set; }
    public int TargetPct { get; set; }
    public int Schools { get; set; }
    public int WinRate { get; set; }
    public int ActiveLeads { get; set; }
    public int FoCount { get; set; }
    public decimal Forecast { get; set; }
    public string Health { get; set; } = string.Empty;
}

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class LossReasonDto
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ScaDashboardDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalUsers { get; set; }
    public int TotalLeads { get; set; }
    public int TotalDeals { get; set; }
    public int TotalSchoolsWon { get; set; }
    public int DealsLost { get; set; }
    public decimal PipelineValue { get; set; }
    public int ActiveLeads { get; set; }
    public int WinRate { get; set; }
    public int TotalPayments { get; set; }
    public decimal TotalPaymentAmount { get; set; }

    // Activity counts (this month)
    public int VisitsThisMonth { get; set; }
    public int DemosThisMonth { get; set; }

    public List<RoleSummaryDto> RoleSummaries { get; set; } = new();
    public List<RegionSummaryDto> Regions { get; set; } = new();
    public List<ChartDataPoint> RevenueChart { get; set; } = new();
    public List<FunnelStage> ConversionFunnel { get; set; } = new();
    public List<AgingDeal> AgingDeals { get; set; } = new();
    public List<LossReasonDto> LossReasons { get; set; } = new();
}

public class RoleSummaryDto
{
    public string Role { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ActiveLeads { get; set; }
    public int DealsWon { get; set; }
    public decimal Revenue { get; set; }
    public int TotalActivities { get; set; }
}

public class UserPerformanceDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Zone { get; set; }
    public string? Region { get; set; }
    public int TotalLeads { get; set; }
    public int ActiveLeads { get; set; }
    public int WonLeads { get; set; }
    public int LostLeads { get; set; }
    public int TotalDeals { get; set; }
    public int ApprovedDeals { get; set; }
    public decimal Revenue { get; set; }
    public decimal Target { get; set; }
    public int TargetPct { get; set; }
    public int WinRate { get; set; }
    public int TotalActivities { get; set; }
    public int VisitsThisMonth { get; set; }
    public int DemosThisMonth { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, int> LeadsByStage { get; set; } = new();
}

public class ReportableUserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string? Region { get; set; }
}
