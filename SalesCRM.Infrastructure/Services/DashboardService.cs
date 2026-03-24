using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FoDashboardDto> GetFoDashboardAsync(int foId)
    {
        var now = DateTime.UtcNow;
        var weekStart = now.AddDays(-(int)now.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

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

        var hotLeads = leads
            .Where(l => l.Score >= 70 && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost)
            .OrderByDescending(l => l.Score)
            .Take(5)
            .ToList();

        return new FoDashboardDto
        {
            Revenue = wonDeals.Sum(d => d.FinalValue),
            RevenueTarget = 2000000,
            VisitsThisWeek = activities.Count(a => a.Type == ActivityType.Visit && a.Date >= weekStart),
            DemosThisMonth = activities.Count(a => a.Type == ActivityType.Demo && a.Date >= monthStart),
            DealsWon = wonDeals.Count,
            PipelineLeads = leads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost),
            PipelineValue = leads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).Sum(l => l.Value),
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

    public async Task<ZoneDashboardDto> GetZoneDashboardAsync(int zhId)
    {
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

        var pendingDeals = deals.Where(d => d.ApprovalStatus == ApprovalStatus.PendingZH).ToList();
        var wonDeals = deals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var totalDeals = deals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
        var activeLeads = leads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost);

        var foPerformance = await GetTeamPerformanceAsync(zhId);

        return new ZoneDashboardDto
        {
            ZoneName = zh.Zone?.Name ?? string.Empty,
            RevenueMTD = wonDeals.Sum(d => d.FinalValue),
            RevenueTarget = 8000000,
            TargetPct = wonDeals.Sum(d => d.FinalValue) > 0 ? (int)(wonDeals.Sum(d => d.FinalValue) * 100 / 8000000) : 0,
            ActivePipeline = activeLeads,
            PendingApprovals = pendingDeals.Count,
            WinRate = totalDeals > 0 ? wonDeals.Count * 100 / totalDeals : 0,
            AtRiskFOs = foPerformance.Count(f => f.Status == "At Risk" || f.Status == "Underperforming"),
            FoPerformance = foPerformance,
            PendingDeals = pendingDeals.Select(d => new DealDto
            {
                Id = d.Id, LeadId = d.LeadId, School = d.Lead?.School ?? string.Empty,
                FoId = d.FoId, FoName = d.Fo?.Name ?? string.Empty,
                ContractValue = d.ContractValue, Discount = d.Discount,
                FinalValue = d.FinalValue, ApprovalStatus = d.ApprovalStatus.ToString(),
                SubmittedAt = d.SubmittedAt
            }).ToList()
        };
    }

    public async Task<RegionDashboardDto> GetRegionDashboardAsync(int rhId)
    {
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
            .Include(d => d.Fo)
            .Where(d => foIds.Contains(d.FoId))
            .ToListAsync();

        var wonDeals = deals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var activeLeads = leads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost);
        var totalSubmitted = deals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
        var totalRevenue = wonDeals.Sum(d => d.FinalValue);

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
                Pipeline = zoneActive, Health = health
            });
        }

        return new RegionDashboardDto
        {
            RegionName = rh.Region?.Name ?? string.Empty,
            RevenueMTD = totalRevenue,
            RevenueTarget = 40000000,
            TargetPct = totalRevenue > 0 ? (int)(totalRevenue * 100 / 40000000) : 0,
            ActiveLeads = activeLeads,
            DealsWon = wonDeals.Count,
            WinRate = totalSubmitted > 0 ? wonDeals.Count * 100 / totalSubmitted : 0,
            Zones = zoneSummaries
        };
    }

    public async Task<NationalDashboardDto> GetNationalDashboardAsync()
    {
        var regions = await _unitOfWork.Regions.GetAllAsync();
        var allUsers = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.FO)
            .ToListAsync();
        var allDeals = await _unitOfWork.Deals.Query()
            .Include(d => d.Fo)
            .ToListAsync();

        var wonDeals = allDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var allLeads = await _unitOfWork.Leads.GetAllAsync();
        var activeLeads = allLeads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();
        var totalSubmitted = allDeals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);

        var lostLeads = allLeads.Where(l => l.Stage == LeadStage.Lost && l.LossReason != null)
            .GroupBy(l => l.LossReason!)
            .Select(g => new LossReasonDto { Reason = g.Key, Count = g.Count() })
            .OrderByDescending(l => l.Count)
            .ToList();

        var totalRevenue = wonDeals.Sum(d => d.FinalValue);

        // Build region summaries with full data
        var regionSummaries = new List<RegionSummaryDto>();
        foreach (var region in regions)
        {
            var regionFoIds = allUsers.Where(u => u.RegionId == region.Id).Select(u => u.Id).ToList();
            var regionDeals = allDeals.Where(d => regionFoIds.Contains(d.FoId)).ToList();
            var regionWon = regionDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
            var regionLeads = allLeads.Where(l => regionFoIds.Contains(l.FoId)).ToList();
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
                Health = health
            });
        }

        return new NationalDashboardDto
        {
            RevenueMTD = totalRevenue,
            RevenueTarget = 200000000,
            TargetPct = totalRevenue > 0 ? (int)(totalRevenue * 100 / 200000000) : 0,
            SchoolsWon = wonDeals.Count,
            PipelineValue = activeLeads.Sum(l => l.Value),
            WinRate = totalSubmitted > 0 ? wonDeals.Count * 100 / totalSubmitted : 0,
            Regions = regionSummaries,
            LossReasons = lostLeads
        };
    }

    public async Task<ScaDashboardDto> GetScaDashboardAsync()
    {
        var allUsers = await _unitOfWork.Users.GetAllAsync();
        var allLeads = await _unitOfWork.Leads.GetAllAsync();
        var allDeals = await _unitOfWork.Deals.Query().Include(d => d.Fo).ToListAsync();
        var allActivities = await _unitOfWork.Activities.GetAllAsync();
        var allPayments = await _unitOfWork.Payments.GetAllAsync();
        var regions = await _unitOfWork.Regions.GetAllAsync();

        var wonDeals = allDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var activeLeads = allLeads.Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost).ToList();
        var totalSubmitted = allDeals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);
        var totalRevenue = wonDeals.Sum(d => d.FinalValue);

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

            // For FO: direct leads/deals. For managers: leads/deals of FOs under them
            List<int> foIds;
            if (role == UserRole.FO)
                foIds = roleUserIds;
            else
                foIds = allUsers.Where(u => u.Role == UserRole.FO).Select(u => u.Id).ToList();

            var roleLeads = allLeads.Where(l => foIds.Contains(l.FoId)).ToList();
            var roleDeals = allDeals.Where(d => foIds.Contains(d.FoId)).ToList();
            var roleActivities = allActivities.Where(a => foIds.Contains(a.FoId)).ToList();

            roleSummaries.Add(new RoleSummaryDto
            {
                Role = role.ToString(),
                RoleLabel = roleLabels.GetValueOrDefault(role.ToString(), role.ToString()),
                Count = roleUsers.Count,
                ActiveLeads = role == UserRole.FO ? roleLeads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost) : 0,
                DealsWon = role == UserRole.FO ? roleDeals.Count(d => d.ApprovalStatus == ApprovalStatus.Approved) : 0,
                Revenue = role == UserRole.FO ? roleDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).Sum(d => d.FinalValue) : 0,
                TotalActivities = role == UserRole.FO ? roleActivities.Count : 0
            });
        }

        // Region summaries
        var foUsers = allUsers.Where(u => u.Role == UserRole.FO).ToList();
        var regionSummaries = regions.Select(region =>
        {
            var regionFoIds = foUsers.Where(u => u.RegionId == region.Id).Select(u => u.Id).ToList();
            var regionDeals = allDeals.Where(d => regionFoIds.Contains(d.FoId)).ToList();
            var regionWon = regionDeals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
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
                Health = health
            };
        }).ToList();

        return new ScaDashboardDto
        {
            TotalRevenue = totalRevenue,
            TotalUsers = allUsers.Count(u => u.Role != UserRole.SCA),
            TotalLeads = allLeads.Count,
            TotalDeals = allDeals.Count,
            TotalSchoolsWon = wonDeals.Count,
            PipelineValue = activeLeads.Sum(l => l.Value),
            WinRate = totalSubmitted > 0 ? wonDeals.Count * 100 / totalSubmitted : 0,
            TotalPayments = allPayments.Count,
            TotalPaymentAmount = allPayments.Sum(p => p.Amount),
            RoleSummaries = roleSummaries,
            Regions = regionSummaries
        };
    }

    public async Task<List<FoPerformanceDto>> GetTeamPerformanceAsync(int zhId)
    {
        var zh = await _unitOfWork.Users.GetByIdAsync(zhId);
        if (zh?.ZoneId == null) return new();

        var now = DateTime.UtcNow;
        var weekStart = now.AddDays(-(int)now.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var zoneFos = await _unitOfWork.Users.Query()
            .Where(u => u.ZoneId == zh.ZoneId && u.Role == UserRole.FO)
            .ToListAsync();

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

        // For FOs: data is directly on them (FoId)
        // For managers (ZH/RH): aggregate data from all FOs under them
        var allFos = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.FO)
            .ToListAsync();

        var allLeads = await _unitOfWork.Leads.Query().ToListAsync();
        var allDeals = await _unitOfWork.Deals.Query().ToListAsync();
        var allActivities = await _unitOfWork.Activities.Query().ToListAsync();
        var allZones = await _unitOfWork.Zones.Query().ToListAsync();

        var result = new List<UserPerformanceDto>();

        foreach (var u in users)
        {
            // Determine which FO IDs this user's data comes from
            List<int> foIds;
            decimal target;

            if (u.Role == UserRole.FO)
            {
                foIds = new List<int> { u.Id };
                target = 2000000;
            }
            else if (u.Role == UserRole.ZH)
            {
                // ZH: aggregate all FOs in their zone
                foIds = allFos.Where(f => f.ZoneId == u.ZoneId).Select(f => f.Id).ToList();
                target = 8000000;
            }
            else if (u.Role == UserRole.RH)
            {
                // RH: aggregate all FOs in their region
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

            // Build territory/scope info
            string? territory = u.Role switch
            {
                UserRole.ZH => u.Zone?.Name,
                UserRole.RH => u.Region?.Name,
                _ => u.Zone?.Name
            };

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
}
