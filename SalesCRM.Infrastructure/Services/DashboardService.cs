using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAllowanceConfigService _allowanceConfigService;

    public DashboardService(IUnitOfWork unitOfWork, IAllowanceConfigService allowanceConfigService)
    {
        _unitOfWork = unitOfWork;
        _allowanceConfigService = allowanceConfigService;
    }

    // ───────── Period helper ─────────

    private static (DateTime periodStart, DateTime periodEnd, DateTime weekStart, DateTime monthStart) GetPeriodDates(string period)
    {
        var now = DateTime.UtcNow;
        var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var weekStart = now.AddDays(-(int)now.DayOfWeek == 0 ? -6 : (1 - (int)now.DayOfWeek));
        weekStart = DateTime.SpecifyKind(weekStart.Date, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return period?.ToLower() switch
        {
            "today" => (todayStart, now, weekStart, monthStart),
            "week" => (weekStart, now, weekStart, monthStart),
            "month" => (monthStart, now, weekStart, monthStart),
            _ => (monthStart, now, weekStart, monthStart),
        };
    }

    // ───────── Shared helpers ─────────

    private static List<FunnelStage> BuildFunnel(IEnumerable<Core.Entities.Lead> leads)
    {
        var list = leads.ToList();
        return new List<FunnelStage>
        {
            new() { Stage = "New Lead",     Count = list.Count(l => l.Stage == LeadStage.NewLead),     Value = list.Where(l => l.Stage == LeadStage.NewLead).Sum(l => l.Value) },
            new() { Stage = "Contacted",    Count = list.Count(l => l.Stage == LeadStage.Contacted),    Value = list.Where(l => l.Stage == LeadStage.Contacted).Sum(l => l.Value) },
            new() { Stage = "Qualified",    Count = list.Count(l => l.Stage == LeadStage.Qualified),    Value = list.Where(l => l.Stage == LeadStage.Qualified).Sum(l => l.Value) },
            new() { Stage = "Demo",         Count = list.Count(l => l.Stage == LeadStage.DemoStage || l.Stage == LeadStage.DemoDone), Value = list.Where(l => l.Stage == LeadStage.DemoStage || l.Stage == LeadStage.DemoDone).Sum(l => l.Value) },
            new() { Stage = "Proposal",     Count = list.Count(l => l.Stage == LeadStage.ProposalSent), Value = list.Where(l => l.Stage == LeadStage.ProposalSent).Sum(l => l.Value) },
            new() { Stage = "Negotiation",  Count = list.Count(l => l.Stage == LeadStage.Negotiation || l.Stage == LeadStage.ContractSent), Value = list.Where(l => l.Stage == LeadStage.Negotiation || l.Stage == LeadStage.ContractSent).Sum(l => l.Value) },
            new() { Stage = "Won",          Count = list.Count(l => l.Stage == LeadStage.Won),          Value = list.Where(l => l.Stage == LeadStage.Won).Sum(l => l.Value) },
        };
    }

    private static List<AgingDeal> BuildAgingDeals(IEnumerable<Core.Entities.Deal> deals, DateTime now, int take = 5)
    {
        return deals
            .Where(d => d.ApprovalStatus != ApprovalStatus.Approved && d.ApprovalStatus != ApprovalStatus.Rejected)
            .Select(d =>
            {
                var days = (int)(now - d.CreatedAt).TotalDays;
                return new AgingDeal
                {
                    School = d.Lead?.School ?? "",
                    Value = d.FinalValue,
                    Stage = d.ApprovalStatus.ToString(),
                    DaysInStage = days,
                    Risk = days > 14 ? "HIGH" : days > 7 ? "MEDIUM" : "LOW"
                };
            })
            .OrderByDescending(d => d.DaysInStage)
            .Take(take)
            .ToList();
    }

    private static List<ChartDataPoint> BuildRevenueChart(IEnumerable<Core.Entities.Deal> wonDeals, int months = 6)
    {
        var now = DateTime.UtcNow;
        var chart = new List<ChartDataPoint>();
        for (int i = months - 1; i >= 0; i--)
        {
            var d = now.AddMonths(-i);
            var mStart = new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var mEnd = mStart.AddMonths(1);
            var revenue = wonDeals.Where(deal => deal.CreatedAt >= mStart && deal.CreatedAt < mEnd).Sum(deal => deal.FinalValue);
            chart.Add(new ChartDataPoint { Label = mStart.ToString("MMM"), Value = revenue });
        }
        return chart;
    }

    private static List<LossReasonDto> BuildLossReasons(IEnumerable<Core.Entities.Lead> leads)
    {
        return leads
            .Where(l => l.Stage == LeadStage.Lost && l.LossReason != null)
            .GroupBy(l => l.LossReason!)
            .Select(g => new LossReasonDto { Reason = g.Key, Count = g.Count() })
            .OrderByDescending(l => l.Count)
            .ToList();
    }

    // ───────── FO Dashboard ─────────

    public async Task<FoDashboardDto> GetFoDashboardAsync(int foId, string period = "today")
    {
        var now = DateTime.UtcNow;
        var (periodStart, periodEnd, weekStart, monthStart) = GetPeriodDates(period);

        var leads = await _unitOfWork.Leads.Query()
            .Where(l => l.FoId == foId)
            .ToListAsync();

        var activities = await _unitOfWork.Activities.Query()
            .Include(a => a.Lead)
            .Where(a => a.FoId == foId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        var wonDeals = await _unitOfWork.Deals.Query()
            .Where(d => d.FoId == foId && d.ApprovalStatus == ApprovalStatus.Approved)
            .ToListAsync();

        var tasks = await _unitOfWork.Tasks.Query()
            .Where(t => t.UserId == foId && t.ScheduledTime.Date == now.Date)
            .OrderBy(t => t.ScheduledTime)
            .ToListAsync();

        // Get FO user for allowance rate
        var foUser = await _unitOfWork.Users.GetByIdAsync(foId);

        var hotLeads = leads
            .Where(l => l.Score >= 70 && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost)
            .OrderByDescending(l => l.Score)
            .Take(5)
            .ToList();

        // Today's tracking session
        var todayUtc = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var todaySession = await _unitOfWork.TrackingSessions.Query()
            .Where(s => s.UserId == foId && s.SessionDate.Date == todayUtc.Date)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync();

        decimal hoursWorked = 0, distKm = 0, allowance = 0;
        if (todaySession?.StartedAt != null)
        {
            hoursWorked = (decimal)((todaySession.EndedAt ?? now) - todaySession.StartedAt.Value).TotalHours;
            distKm = todaySession.TotalDistanceKm;
            allowance = todaySession.AllowanceAmount;
        }

        // Time breakdown from visit logs
        var todayVisitLogs = await _unitOfWork.SchoolVisitLogs.Query()
            .Where(v => v.UserId == foId && v.VisitDate.Date == todayUtc.Date).ToListAsync();
        var inSchoolMins = todayVisitLogs.Sum(v => v.DurationMinutes ?? 0);
        var totalMins = hoursWorked * 60;
        var travelMins = Math.Max(0, totalMins * 0.3m);
        var idleMins = Math.Max(0, totalMins - inSchoolMins - travelMins);

        // Conversion funnel
        var funnel = BuildFunnel(leads);

        // Deal aging
        var pendingDeals = await _unitOfWork.Deals.Query().Include(d => d.Lead)
            .Where(d => d.FoId == foId && d.ApprovalStatus != ApprovalStatus.Approved && d.ApprovalStatus != ApprovalStatus.Rejected)
            .ToListAsync();
        var agingDeals = BuildAgingDeals(pendingDeals, now);

        // Rate to display in the Allowance tile: prefer the session's stored rate
        // (set when day was started, already resolved via AllowanceConfig). Otherwise
        // resolve live so the tile reflects role/user configs even before Start Day.
        decimal allowanceRate = todaySession?.AllowanceRatePerKm
            ?? (await _allowanceConfigService.ResolveForUserAsync(foId)).RatePerKm;

        return new FoDashboardDto
        {
            Revenue = wonDeals.Sum(d => d.FinalValue),
            RevenueTarget = 2000000,
            VisitsThisWeek = activities.Count(a => a.Type == ActivityType.Visit && a.Date >= weekStart),
            DemosThisMonth = activities.Count(a => a.Type == ActivityType.Demo && a.Date >= periodStart),
            VisitsThisMonth = activities.Count(a => a.Type == ActivityType.Visit && a.Date >= periodStart),
            FollowUpsThisMonth = activities.Count(a => a.Type == ActivityType.FollowUp && a.Date >= periodStart),
            DealsWon = wonDeals.Count,
            DealsLost = leads.Count(l => l.Stage == LeadStage.Lost),
            PipelineLeads = leads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost),
            PipelineValue = leads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).Sum(l => l.Value),
            HoursWorked = Math.Round(hoursWorked, 1),
            TotalDistanceKm = Math.Round(distKm, 1),
            AllowanceAmount = Math.Round(allowance, 0),
            InSchoolMinutes = Math.Round(inSchoolMins, 0),
            TravellingMinutes = Math.Round(travelMins, 0),
            IdleMinutes = Math.Round(idleMins, 0),
            // Activity targets — centralized in backend, not hardcoded in frontend
            VisitsTargetWeekly = 15,
            VisitsTargetMonthly = 80,
            DemosTargetMonthly = 28,
            FollowUpsTargetMonthly = 40,
            DealsTargetMonthly = 5,
            AllowanceRatePerKm = allowanceRate,
            ConversionFunnel = funnel,
            AgingDeals = agingDeals,
            HotLeads = hotLeads.Select(l => new LeadListDto
            {
                Id = l.Id, School = l.School, Board = l.Board, City = l.City,
                Type = l.Type, Stage = l.Stage.ToString(), Score = l.Score,
                Value = l.Value, LastActivityDate = l.LastActivityDate,
                Source = l.Source, FoId = l.FoId, ContactName = l.ContactName
            }).ToList(),
            TodaysTasks = tasks.Select(t => new TaskItemDto
            {
                Id = t.Id, ScheduledTime = t.ScheduledTime, Type = t.Type.ToString(),
                School = t.School, IsDone = t.IsDone, LeadId = t.LeadId
            }).ToList(),
            RecentActivities = activities.Take(10).Select(a => new ActivityDto
            {
                Id = a.Id, Type = a.Type.ToString(), Date = a.Date,
                Outcome = a.Outcome.ToString(), Notes = a.Notes,
                GpsVerified = a.GpsVerified, FoId = a.FoId,
                LeadId = a.LeadId, School = a.Lead?.School ?? string.Empty
            }).ToList()
        };
    }

    // ───────── Zone (ZH) Dashboard ─────────

    public async Task<ZoneDashboardDto> GetZoneDashboardAsync(int zhId, string period = "month")
    {
        var now = DateTime.UtcNow;
        var (periodStart, periodEnd, weekStart, monthStart) = GetPeriodDates(period);

        var zh = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .FirstOrDefaultAsync(u => u.Id == zhId);

        if (zh?.ZoneId == null) return new ZoneDashboardDto();

        var zoneFos = await _unitOfWork.Users.Query()
            .Where(u => u.ZoneId == zh.ZoneId && u.Role == UserRole.FO)
            .ToListAsync();

        var foIds = zoneFos.Select(f => f.Id).ToList();

        var leads = await _unitOfWork.Leads.Query()
            .Where(l => foIds.Contains(l.FoId))
            .ToListAsync();

        var deals = await _unitOfWork.Deals.Query()
            .Include(d => d.Lead).Include(d => d.Fo)
            .Where(d => foIds.Contains(d.FoId))
            .ToListAsync();

        var activities = await _unitOfWork.Activities.Query()
            .Where(a => foIds.Contains(a.FoId))
            .ToListAsync();

        var pendingDeals = deals.Where(d => d.ApprovalStatus == ApprovalStatus.PendingZH).ToList();
        var wonDeals = deals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var totalDeals = deals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
        var activeLeads = leads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();

        var foPerformance = await GetTeamPerformanceAsync(zhId, "ZH");

        return new ZoneDashboardDto
        {
            ZoneName = zh.Zone?.Name ?? string.Empty,
            RevenueMTD = wonDeals.Sum(d => d.FinalValue),
            RevenueTarget = 8000000,
            TargetPct = wonDeals.Sum(d => d.FinalValue) > 0 ? (int)(wonDeals.Sum(d => d.FinalValue) * 100 / 8000000) : 0,
            ActivePipeline = activeLeads.Count,
            PipelineValue = activeLeads.Sum(l => l.Value),
            PendingApprovals = pendingDeals.Count,
            WinRate = totalDeals > 0 ? wonDeals.Count * 100 / totalDeals : 0,
            AtRiskFOs = foPerformance.Count(f => f.Status == "At Risk" || f.Status == "Underperforming"),
            TotalFOs = zoneFos.Count,
            DealsLost = leads.Count(l => l.Stage == LeadStage.Lost),
            VisitsThisMonth = activities.Count(a => a.Type == ActivityType.Visit && a.Date >= periodStart),
            DemosThisMonth = activities.Count(a => a.Type == ActivityType.Demo && a.Date >= periodStart),
            CallsThisMonth = activities.Count(a => a.Type == ActivityType.Call && a.Date >= periodStart),
            // Zone targets = per-FO target × number of FOs
            VisitsTargetMonthly = zoneFos.Count * 80,
            DemosTargetMonthly = zoneFos.Count * 28,
            CallsTargetMonthly = zoneFos.Count * 200,
            FoPerformance = foPerformance,
            PendingDeals = pendingDeals.Select(d => new DealDto
            {
                Id = d.Id, LeadId = d.LeadId, School = d.Lead?.School ?? string.Empty,
                FoId = d.FoId, FoName = d.Fo?.Name ?? string.Empty,
                ContractValue = d.ContractValue, Discount = d.Discount,
                FinalValue = d.FinalValue, ApprovalStatus = d.ApprovalStatus.ToString(),
                SubmittedAt = d.SubmittedAt
            }).ToList(),
            ConversionFunnel = BuildFunnel(leads),
            AgingDeals = BuildAgingDeals(deals, now),
            RevenueChart = BuildRevenueChart(wonDeals),
        };
    }

    // ───────── Region (RH) Dashboard ─────────

    public async Task<RegionDashboardDto> GetRegionDashboardAsync(int rhId, string period = "month")
    {
        var now = DateTime.UtcNow;
        var (periodStart, periodEnd, weekStart, monthStart) = GetPeriodDates(period);

        var rh = await _unitOfWork.Users.Query()
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == rhId);

        if (rh?.RegionId == null) return new RegionDashboardDto();

        var zones = await _unitOfWork.Zones.Query()
            .Where(z => z.RegionId == rh.RegionId)
            .ToListAsync();

        var regionUsers = await _unitOfWork.Users.Query()
            .Where(u => u.RegionId == rh.RegionId && u.Role == UserRole.FO)
            .ToListAsync();

        var foIds = regionUsers.Select(u => u.Id).ToList();

        var leads = await _unitOfWork.Leads.Query()
            .Where(l => foIds.Contains(l.FoId))
            .ToListAsync();

        var deals = await _unitOfWork.Deals.Query()
            .Include(d => d.Fo).Include(d => d.Lead)
            .Where(d => foIds.Contains(d.FoId))
            .ToListAsync();

        var activities = await _unitOfWork.Activities.Query()
            .Where(a => foIds.Contains(a.FoId))
            .ToListAsync();

        var wonDeals = deals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var activeLeads = leads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();
        var totalSubmitted = deals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
        var totalRevenue = wonDeals.Sum(d => d.FinalValue);
        var pendingCount = deals.Count(d => d.ApprovalStatus == ApprovalStatus.PendingRH);

        // Forecast accuracy: compare last month's pipeline value to this month's closed revenue
        var lastMonthStart = monthStart.AddMonths(-1);
        var lastMonthPipeline = leads.Where(l => l.CreatedAt < monthStart).Sum(l => l.Value);
        var forecastAccuracy = lastMonthPipeline > 0 ? Math.Min(100, (int)(totalRevenue * 100 / lastMonthPipeline)) : 0;

        // Build zone summaries with full data
        var zoneSummaries = new List<ZoneSummaryDto>();
        foreach (var zone in zones)
        {
            var zoneFoIds = regionUsers.Where(u => u.ZoneId == zone.Id).Select(u => u.Id).ToList();
            var zoneLeads = leads.Where(l => zoneFoIds.Contains(l.FoId)).ToList();
            var zoneDeals = deals.Where(d => zoneFoIds.Contains(d.FoId)).ToList();
            var zoneWon = zoneDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
            var zoneSubmitted = zoneDeals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
            var zoneActive = zoneLeads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost);
            decimal zoneTarget = 8000000;
            var zoneRevenue = zoneWon.Sum(d => d.FinalValue);
            var zonePct = zoneTarget > 0 ? (int)(zoneRevenue * 100 / zoneTarget) : 0;
            var zoneWinRate = zoneSubmitted > 0 ? zoneWon.Count * 100 / zoneSubmitted : 0;
            string health = zonePct >= 50 ? "Strong" : zonePct >= 30 ? "Good" : zonePct >= 15 ? "At Risk" : "Weak";

            zoneSummaries.Add(new ZoneSummaryDto
            {
                Id = zone.Id, Name = zone.Name,
                Revenue = zoneRevenue, Target = zoneTarget,
                TargetPct = zonePct, WinRate = zoneWinRate,
                Pipeline = zoneActive, Health = health,
                FoCount = zoneFoIds.Count,
                DealsWon = zoneWon.Count,
            });
        }

        return new RegionDashboardDto
        {
            RegionName = rh.Region?.Name ?? string.Empty,
            RevenueMTD = totalRevenue,
            RevenueTarget = 40000000,
            TargetPct = totalRevenue > 0 ? (int)(totalRevenue * 100 / 40000000) : 0,
            ActiveLeads = activeLeads.Count,
            PipelineValue = activeLeads.Sum(l => l.Value),
            DealsWon = wonDeals.Count,
            DealsLost = leads.Count(l => l.Stage == LeadStage.Lost),
            WinRate = totalSubmitted > 0 ? wonDeals.Count * 100 / totalSubmitted : 0,
            ForecastAccuracy = forecastAccuracy,
            TotalFOs = regionUsers.Count,
            TotalZones = zones.Count,
            PendingApprovals = pendingCount,
            VisitsThisMonth = activities.Count(a => a.Type == ActivityType.Visit && a.Date >= periodStart),
            DemosThisMonth = activities.Count(a => a.Type == ActivityType.Demo && a.Date >= periodStart),
            Zones = zoneSummaries,
            RevenueChart = BuildRevenueChart(wonDeals),
            ConversionFunnel = BuildFunnel(leads),
            AgingDeals = BuildAgingDeals(deals, now),
            LossReasons = BuildLossReasons(leads),
        };
    }

    // ───────── National (SH) Dashboard ─────────

    public async Task<NationalDashboardDto> GetNationalDashboardAsync(string period = "month")
    {
        var now = DateTime.UtcNow;
        var (periodStart, periodEnd, weekStart, monthStart) = GetPeriodDates(period);

        var regions = await _unitOfWork.Regions.GetAllAsync();
        var zones = await _unitOfWork.Zones.Query().ToListAsync();
        var allUsers = await _unitOfWork.Users.Query().ToListAsync();
        var foUsers = allUsers.Where(u => u.Role == UserRole.FO).ToList();
        var allDeals = await _unitOfWork.Deals.Query()
            .Include(d => d.Fo).Include(d => d.Lead)
            .ToListAsync();

        var wonDeals = allDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var allLeads = await _unitOfWork.Leads.GetAllAsync();
        var activeLeads = allLeads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();
        var totalSubmitted = allDeals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
        var pendingCount = allDeals.Count(d =>
            d.ApprovalStatus == ApprovalStatus.PendingSH);

        var allActivities = await _unitOfWork.Activities.GetAllAsync();

        var totalRevenue = wonDeals.Sum(d => d.FinalValue);

        // Build region summaries with full data
        var regionSummaries = new List<RegionSummaryDto>();
        foreach (var region in regions)
        {
            var regionFoIds = foUsers.Where(u => u.RegionId == region.Id).Select(u => u.Id).ToList();
            var regionDeals = allDeals.Where(d => regionFoIds.Contains(d.FoId)).ToList();
            var regionWon = regionDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
            var regionLeads = allLeads.Where(l => regionFoIds.Contains(l.FoId)).ToList();
            var regionActive = regionLeads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();
            var regionSubmitted = regionDeals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
            decimal regionTarget = 40000000;
            var regionRevenue = regionWon.Sum(d => d.FinalValue);
            var regionPct = regionTarget > 0 ? (int)(regionRevenue * 100 / regionTarget) : 0;
            var regionWinRate = regionSubmitted > 0 ? regionWon.Count * 100 / regionSubmitted : 0;
            string health = regionPct >= 40 ? "Strong" : regionPct >= 25 ? "Good" : regionPct >= 15 ? "At Risk" : "Weak";

            regionSummaries.Add(new RegionSummaryDto
            {
                Id = region.Id, Name = region.Name,
                Revenue = regionRevenue, Target = regionTarget,
                TargetPct = regionPct, Schools = regionWon.Count,
                WinRate = regionWinRate, Forecast = regionRevenue * 1.2m,
                Health = health,
                ActiveLeads = regionActive.Count,
                FoCount = regionFoIds.Count,
            });
        }

        // Top 5 FO performers nationally
        var topPerformers = new List<FoPerformanceDto>();
        foreach (var fo in foUsers)
        {
            var foDeals = allDeals.Where(d => d.FoId == fo.Id && d.ApprovalStatus == ApprovalStatus.Approved).ToList();
            var foLeads = allLeads.Where(l => l.FoId == fo.Id && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).Count();
            var foActivities = allActivities.Where(a => a.FoId == fo.Id).ToList();
            var revenue = foDeals.Sum(d => d.FinalValue);
            decimal target = 2000000;
            var targetPct = target > 0 ? (int)(revenue * 100 / target) : 0;
            topPerformers.Add(new FoPerformanceDto
            {
                FoId = fo.Id, Name = fo.Name, Avatar = fo.Avatar,
                Revenue = revenue, Target = target, TargetPct = targetPct,
                VisitsWeek = foActivities.Count(a => a.Type == ActivityType.Visit && a.Date >= weekStart),
                DemosMonth = foActivities.Count(a => a.Type == ActivityType.Demo && a.Date >= monthStart),
                DealsWon = foDeals.Count, PipelineLeads = foLeads,
                Status = targetPct >= 70 ? "On Track" : targetPct >= 35 ? "At Risk" : "Underperforming"
            });
        }

        return new NationalDashboardDto
        {
            RevenueMTD = totalRevenue,
            RevenueTarget = 200000000,
            TargetPct = totalRevenue > 0 ? (int)(totalRevenue * 100 / 200000000) : 0,
            SchoolsWon = wonDeals.Count,
            DealsLost = allLeads.Count(l => l.Stage == LeadStage.Lost),
            PipelineValue = activeLeads.Sum(l => l.Value),
            WinRate = totalSubmitted > 0 ? wonDeals.Count * 100 / totalSubmitted : 0,
            ActiveLeads = activeLeads.Count,
            TotalFOs = foUsers.Count,
            TotalZones = zones.Count,
            TotalRegions = regions.Count(),
            PendingApprovals = pendingCount,
            VisitsThisMonth = allActivities.Count(a => a.Type == ActivityType.Visit && a.Date >= periodStart),
            DemosThisMonth = allActivities.Count(a => a.Type == ActivityType.Demo && a.Date >= periodStart),
            Regions = regionSummaries,
            RevenueChart = BuildRevenueChart(wonDeals),
            LossReasons = BuildLossReasons(allLeads),
            ConversionFunnel = BuildFunnel(allLeads),
            AgingDeals = BuildAgingDeals(allDeals, now, 10),
            TopPerformers = topPerformers.OrderByDescending(f => f.TargetPct).Take(5).ToList(),
        };
    }

    // ───────── SCA Dashboard ─────────

    public async Task<ScaDashboardDto> GetScaDashboardAsync(string period = "month")
    {
        var now = DateTime.UtcNow;
        var (periodStart, periodEnd, weekStart, monthStart) = GetPeriodDates(period);

        var allUsers = await _unitOfWork.Users.GetAllAsync();
        var allLeads = await _unitOfWork.Leads.GetAllAsync();
        var allDeals = await _unitOfWork.Deals.Query().Include(d => d.Fo).Include(d => d.Lead).ToListAsync();
        var allActivities = await _unitOfWork.Activities.GetAllAsync();
        var allPayments = await _unitOfWork.Payments.GetAllAsync();
        var regions = await _unitOfWork.Regions.GetAllAsync();
        var allZones = await _unitOfWork.Zones.Query().ToListAsync();

        var wonDeals = allDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var activeLeads = allLeads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();
        var totalSubmitted = allDeals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
        var totalRevenue = wonDeals.Sum(d => d.FinalValue);

        var foUsers = allUsers.Where(u => u.Role == UserRole.FO).ToList();

        var roleLabels = new Dictionary<string, string>
        {
            { "FO", "Field Officers" }, { "ZH", "Zonal Heads" },
            { "RH", "Regional Heads" }, { "SH", "Sales Heads" }
        };

        var roleSummaries = new List<RoleSummaryDto>();
        foreach (var role in new[] { UserRole.FO, UserRole.ZH, UserRole.RH, UserRole.SH })
        {
            var roleUsers = allUsers.Where(u => u.Role == role).ToList();
            var roleUserIds = roleUsers.Select(u => u.Id).ToList();

            // Determine which FO ids to aggregate data from
            List<int> foIds;
            if (role == UserRole.FO)
            {
                foIds = roleUserIds;
            }
            else if (role == UserRole.ZH)
            {
                // ZHs manage FOs in their zones
                var zhZoneIds = roleUsers.Where(u => u.ZoneId.HasValue).Select(u => u.ZoneId!.Value).Distinct().ToList();
                foIds = foUsers.Where(u => u.ZoneId.HasValue && zhZoneIds.Contains(u.ZoneId.Value)).Select(u => u.Id).ToList();
            }
            else if (role == UserRole.RH)
            {
                var rhRegionIds = roleUsers.Where(u => u.RegionId.HasValue).Select(u => u.RegionId!.Value).Distinct().ToList();
                foIds = foUsers.Where(u => u.RegionId.HasValue && rhRegionIds.Contains(u.RegionId.Value)).Select(u => u.Id).ToList();
            }
            else
            {
                // SH — all FOs
                foIds = foUsers.Select(u => u.Id).ToList();
            }

            var roleLeads = allLeads.Where(l => foIds.Contains(l.FoId)).ToList();
            var roleDeals = allDeals.Where(d => foIds.Contains(d.FoId)).ToList();
            var roleActivities = allActivities.Where(a => foIds.Contains(a.FoId)).ToList();
            var roleWonDeals = roleDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();

            roleSummaries.Add(new RoleSummaryDto
            {
                Role = role.ToString(),
                RoleLabel = roleLabels.GetValueOrDefault(role.ToString(), role.ToString()),
                Count = roleUsers.Count,
                ActiveLeads = roleLeads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost),
                DealsWon = roleWonDeals.Count,
                Revenue = roleWonDeals.Sum(d => d.FinalValue),
                TotalActivities = roleActivities.Count,
            });
        }

        // Region summaries
        var regionSummaries = regions.Select(region =>
        {
            var regionFoIds = foUsers.Where(u => u.RegionId == region.Id).Select(u => u.Id).ToList();
            var regionDeals = allDeals.Where(d => regionFoIds.Contains(d.FoId)).ToList();
            var regionWon = regionDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
            var regionLeads = allLeads.Where(l => regionFoIds.Contains(l.FoId)).ToList();
            var regionActive = regionLeads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();
            var regionSubmitted = regionDeals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
            decimal regionTarget = 40000000;
            var regionRevenue = regionWon.Sum(d => d.FinalValue);
            var regionPct = regionTarget > 0 ? (int)(regionRevenue * 100 / regionTarget) : 0;
            var regionWinRate = regionSubmitted > 0 ? regionWon.Count * 100 / regionSubmitted : 0;
            string health = regionPct >= 40 ? "Strong" : regionPct >= 25 ? "Good" : regionPct >= 15 ? "At Risk" : "Weak";

            return new RegionSummaryDto
            {
                Id = region.Id, Name = region.Name,
                Revenue = regionRevenue, Target = regionTarget,
                TargetPct = regionPct, Schools = regionWon.Count,
                WinRate = regionWinRate, Forecast = regionRevenue * 1.2m,
                Health = health,
                ActiveLeads = regionActive.Count,
                FoCount = regionFoIds.Count,
            };
        }).ToList();

        return new ScaDashboardDto
        {
            TotalRevenue = totalRevenue,
            TotalUsers = allUsers.Count(u => u.Role != UserRole.SCA),
            TotalLeads = allLeads.Count,
            TotalDeals = allDeals.Count,
            TotalSchoolsWon = wonDeals.Count,
            DealsLost = allLeads.Count(l => l.Stage == LeadStage.Lost),
            PipelineValue = activeLeads.Sum(l => l.Value),
            ActiveLeads = activeLeads.Count,
            WinRate = totalSubmitted > 0 ? wonDeals.Count * 100 / totalSubmitted : 0,
            TotalPayments = allPayments.Count,
            TotalPaymentAmount = allPayments.Sum(p => p.Amount),
            VisitsThisMonth = allActivities.Count(a => a.Type == ActivityType.Visit && a.Date >= periodStart),
            DemosThisMonth = allActivities.Count(a => a.Type == ActivityType.Demo && a.Date >= periodStart),
            RoleSummaries = roleSummaries,
            Regions = regionSummaries,
            RevenueChart = BuildRevenueChart(wonDeals),
            ConversionFunnel = BuildFunnel(allLeads),
            AgingDeals = BuildAgingDeals(allDeals, now, 10),
            LossReasons = BuildLossReasons(allLeads),
        };
    }

    // ───────── Team Performance (ZH) ─────────

    public async Task<List<FoPerformanceDto>> GetTeamPerformanceAsync(int userId, string userRole)
    {
        var caller = await _unitOfWork.Users.GetByIdAsync(userId);
        var now = DateTime.UtcNow;
        var weekStart = now.AddDays(-(int)now.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var foQuery = _unitOfWork.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .Where(u => u.Role == UserRole.FO);

        if (userRole == "SCA" || userRole == "SH")
        {
            // no additional filter — all FOs
        }
        else if (userRole == "RH" && caller?.RegionId != null)
        {
            foQuery = foQuery.Where(u => u.RegionId == caller.RegionId);
        }
        else if (caller?.ZoneId != null)
        {
            foQuery = foQuery.Where(u => u.ZoneId == caller.ZoneId);
        }
        else
        {
            return new();
        }

        var zoneFos = await foQuery.ToListAsync();

        var result = new List<FoPerformanceDto>();

        foreach (var fo in zoneFos)
        {
            var activities = await _unitOfWork.Activities.Query()
                .Where(a => a.FoId == fo.Id)
                .ToListAsync();

            var deals = await _unitOfWork.Deals.Query()
                .Where(d => d.FoId == fo.Id && d.ApprovalStatus == ApprovalStatus.Approved)
                .ToListAsync();

            var leads = await _unitOfWork.Leads.Query()
                .Where(l => l.FoId == fo.Id && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost)
                .CountAsync();

            var revenue = deals.Sum(d => d.FinalValue);
            decimal target = 2000000;
            var targetPct = target > 0 ? (int)(revenue * 100 / target) : 0;

            var visitsWeek = activities.Count(a => a.Type == ActivityType.Visit && a.Date >= weekStart);
            var demosMonth = activities.Count(a => a.Type == ActivityType.Demo && a.Date >= monthStart);

            string status = targetPct >= 70 ? "On Track" : targetPct >= 35 ? "At Risk" : "Underperforming";

            result.Add(new FoPerformanceDto
            {
                FoId = fo.Id,
                Name = fo.Name,
                Avatar = fo.Avatar,
                Zone = fo.Zone?.Name,
                Region = fo.Region?.Name,
                Revenue = revenue,
                Target = target,
                TargetPct = targetPct,
                VisitsWeek = visitsWeek,
                DemosMonth = demosMonth,
                DealsWon = deals.Count,
                PipelineLeads = leads,
                Status = status
            });
        }

        return result.OrderByDescending(f => f.TargetPct).ToList();
    }

    // ───────── Performance Tracking (all roles) ─────────

    public async Task<List<UserPerformanceDto>> GetPerformanceTrackingAsync(int userId, string userRole)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Show only DIRECT subordinates (one level down)
        var usersQuery = _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .AsQueryable();

        usersQuery = userRole switch
        {
            "FO" => usersQuery.Where(u => u.Id == userId),
            "ZH" => usersQuery.Where(u => u.ZoneId == user.ZoneId && u.Role == UserRole.FO),
            "RH" => usersQuery.Where(u => u.RegionId == user.RegionId && u.Role == UserRole.ZH),
            "SH" => usersQuery.Where(u => u.Role == UserRole.RH),
            "SCA" => usersQuery.Where(u => u.Role != UserRole.SCA),
            _ => usersQuery.Where(u => u.Id == userId)
        };

        var users = await usersQuery.ToListAsync();

        var allFos = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.FO)
            .ToListAsync();

        var allLeads = await _unitOfWork.Leads.Query().ToListAsync();
        var allDeals = await _unitOfWork.Deals.Query().ToListAsync();
        var allActivities = await _unitOfWork.Activities.Query().ToListAsync();

        var result = new List<UserPerformanceDto>();

        foreach (var u in users)
        {
            List<int> foIds;
            decimal target;

            if (u.Role == UserRole.FO)
            {
                foIds = new List<int> { u.Id };
                target = 2000000;
            }
            else if (u.Role == UserRole.ZH)
            {
                foIds = allFos.Where(f => f.ZoneId == u.ZoneId).Select(f => f.Id).ToList();
                target = 8000000;
            }
            else if (u.Role == UserRole.RH)
            {
                foIds = allFos.Where(f => f.RegionId == u.RegionId).Select(f => f.Id).ToList();
                target = 40000000;
            }
            else
            {
                foIds = allFos.Select(f => f.Id).ToList();
                target = 200000000;
            }

            var userLeads = allLeads.Where(l => foIds.Contains(l.FoId)).ToList();
            var userDeals = allDeals.Where(d => foIds.Contains(d.FoId)).ToList();
            var userActivities = allActivities.Where(a => foIds.Contains(a.FoId)).ToList();

            var wonLeads = userLeads.Count(l => l.Stage == LeadStage.Won);
            var lostLeads = userLeads.Count(l => l.Stage == LeadStage.Lost);
            var activeLeads = userLeads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost);
            var approvedDeals = userDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
            var totalSubmitted = userDeals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
            var revenue = approvedDeals.Sum(d => d.FinalValue);
            var targetPct = target > 0 ? (int)(revenue * 100 / target) : 0;
            var winRate = totalSubmitted > 0 ? approvedDeals.Count * 100 / totalSubmitted : 0;

            string status = targetPct >= 70 ? "On Track" : targetPct >= 35 ? "At Risk" : "Needs Attention";

            var leadsByStage = userLeads
                .GroupBy(l => l.Stage.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            result.Add(new UserPerformanceDto
            {
                UserId = u.Id,
                Name = u.Name,
                Role = u.Role.ToString(),
                Avatar = u.Avatar,
                Zone = u.Zone?.Name,
                Region = u.Region?.Name,
                TotalLeads = userLeads.Count,
                ActiveLeads = activeLeads,
                WonLeads = wonLeads,
                LostLeads = lostLeads,
                TotalDeals = userDeals.Count,
                ApprovedDeals = approvedDeals.Count,
                Revenue = revenue,
                Target = target,
                TargetPct = targetPct,
                WinRate = winRate,
                TotalActivities = userActivities.Count,
                VisitsThisMonth = userActivities.Count(a => a.Type == ActivityType.Visit && a.Date >= monthStart),
                DemosThisMonth = userActivities.Count(a => a.Type == ActivityType.Demo && a.Date >= monthStart),
                Status = status,
                LeadsByStage = leadsByStage
            });
        }

        return result.OrderByDescending(u => u.TargetPct).ToList();
    }

    // ───────── Reportable Users (for report generation dropdown) ─────────

    public async Task<List<ReportableUserDto>> GetReportableUsersAsync(int userId, string userRole)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return new();

        var query = _unitOfWork.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .Where(u => u.IsActive);

        query = userRole switch
        {
            // ZH sees FOs in their zone
            "ZH" => query.Where(u => u.ZoneId == user.ZoneId && u.Role == UserRole.FO),
            // RH sees FOs + ZHs in their region
            "RH" => query.Where(u => u.RegionId == user.RegionId && (u.Role == UserRole.FO || u.Role == UserRole.ZH)),
            // SH sees everyone except SCA
            "SH" => query.Where(u => u.Role != UserRole.SH && u.Role != UserRole.SCA),
            // SCA sees everyone except other SCAs
            "SCA" => query.Where(u => u.Role != UserRole.SCA),
            _ => query.Where(u => u.Id == userId),
        };

        var users = await query.OrderBy(u => u.Role).ThenBy(u => u.Name).ToListAsync();

        return users.Select(u => new ReportableUserDto
        {
            Id = u.Id,
            Name = u.Name,
            Role = u.Role.ToString(),
            Zone = u.Zone?.Name,
            Region = u.Region?.Name,
        }).ToList();
    }
}
