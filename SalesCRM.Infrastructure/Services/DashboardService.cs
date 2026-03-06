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
            .Where(d => foIds.Contains(d.FoId))
            .ToListAsync();

        var wonDeals = deals.Where(d => d.ApprovalStatus == ApprovalStatus.Approved).ToList();
        var activeLeads = leads.Count(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost);
        var totalSubmitted = deals.Count(d => d.ApprovalStatus != ApprovalStatus.Draft);

        return new RegionDashboardDto
        {
            RegionName = rh.Region?.Name ?? string.Empty,
            RevenueMTD = wonDeals.Sum(d => d.FinalValue),
            RevenueTarget = 40000000,
            TargetPct = wonDeals.Sum(d => d.FinalValue) > 0 ? (int)(wonDeals.Sum(d => d.FinalValue) * 100 / 40000000) : 0,
            ActiveLeads = activeLeads,
            DealsWon = wonDeals.Count,
            WinRate = totalSubmitted > 0 ? wonDeals.Count * 100 / totalSubmitted : 0,
            Zones = zones.Select(z => new ZoneSummaryDto
            {
                Id = z.Id,
                Name = z.Name,
            }).ToList()
        };
    }

    public async Task<NationalDashboardDto> GetNationalDashboardAsync()
    {
        var regions = await _unitOfWork.Regions.GetAllAsync();
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

        return new NationalDashboardDto
        {
            RevenueMTD = wonDeals.Sum(d => d.FinalValue),
            RevenueTarget = 200000000,
            TargetPct = wonDeals.Sum(d => d.FinalValue) > 0 ? (int)(wonDeals.Sum(d => d.FinalValue) * 100 / 200000000) : 0,
            SchoolsWon = wonDeals.Count,
            PipelineValue = activeLeads.Sum(l => l.Value),
            WinRate = totalSubmitted > 0 ? wonDeals.Count * 100 / totalSubmitted : 0,
            Regions = regions.Select(r => new RegionSummaryDto
            {
                Id = r.Id,
                Name = r.Name
            }).ToList(),
            LossReasons = lostLeads
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
}
