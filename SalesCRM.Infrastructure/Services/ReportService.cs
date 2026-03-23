using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Reports;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    public ReportService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<UserPerformanceDto>> GetUserPerformanceAsync(ReportFilters filters)
    {
        DateTime.TryParse(filters.DateFrom, out var from);
        DateTime.TryParse(filters.DateTo, out var to);
        if (from == default) from = DateTime.UtcNow.AddDays(-30);
        if (to == default) to = DateTime.UtcNow;
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);

        var users = await _uow.Users.Query()
            .Where(u => u.Role == UserRole.FO)
            .ToListAsync();

        if (filters.UserId.HasValue)
            users = users.Where(u => u.Id == filters.UserId.Value).ToList();
        if (filters.ZoneId.HasValue)
            users = users.Where(u => u.ZoneId == filters.ZoneId.Value).ToList();
        if (filters.RegionId.HasValue)
            users = users.Where(u => u.RegionId == filters.RegionId.Value).ToList();

        var userIds = users.Select(u => u.Id).ToList();

        var activities = await _uow.Activities.Query()
            .Where(a => userIds.Contains(a.FoId) && a.Date >= fromUtc && a.Date < toUtc)
            .ToListAsync();

        var demos = await _uow.DemoAssignments.Query()
            .Where(d => userIds.Contains(d.AssignedToId) && d.ScheduledDate >= fromUtc && d.ScheduledDate < toUtc)
            .ToListAsync();

        var deals = await _uow.Deals.Query()
            .Where(d => userIds.Contains(d.FoId) && d.CreatedAt >= fromUtc && d.CreatedAt < toUtc)
            .ToListAsync();

        var sessions = await _uow.TrackingSessions.Query()
            .Where(s => userIds.Contains(s.UserId) && s.SessionDate >= fromUtc && s.SessionDate < toUtc)
            .ToListAsync();

        var visitLogs = await _uow.SchoolVisitLogs.Query()
            .Where(v => userIds.Contains(v.UserId) && v.VisitDate >= fromUtc && v.VisitDate < toUtc)
            .ToListAsync();

        return users.Select(u =>
        {
            var userActivities = activities.Where(a => a.FoId == u.Id).ToList();
            var userDemos = demos.Where(d => d.AssignedToId == u.Id).ToList();
            var userDeals = deals.Where(d => d.FoId == u.Id).ToList();
            var userSessions = sessions.Where(s => s.UserId == u.Id).ToList();
            var userVisits = visitLogs.Where(v => v.UserId == u.Id).ToList();

            return new UserPerformanceDto
            {
                UserId = u.Id, Name = u.Name ?? "", Role = u.Role.ToString(),
                TotalVisits = userActivities.Count(a => a.Type == ActivityType.Visit),
                TotalDemos = userDemos.Count,
                TotalDeals = userDeals.Count,
                TotalRevenue = userDeals.Sum(d => d.FinalValue),
                TotalDistanceKm = userSessions.Sum(s => s.TotalDistanceKm),
                TotalAllowance = userSessions.Sum(s => s.AllowanceAmount),
                AvgVisitDurationMinutes = userVisits.Any(v => v.DurationMinutes.HasValue)
                    ? (decimal)userVisits.Where(v => v.DurationMinutes.HasValue).Average(v => (double)v.DurationMinutes!.Value) : 0,
                SchoolsVisited = userVisits.Select(v => v.SchoolId).Distinct().Count(),
                ConversionRate = userActivities.Count > 0 ? (decimal)userDeals.Count / userActivities.Count * 100 : 0
            };
        }).ToList();
    }

    public async Task<List<SchoolVisitSummaryDto>> GetSchoolVisitSummaryAsync(ReportFilters filters)
    {
        DateTime.TryParse(filters.DateFrom, out var from);
        DateTime.TryParse(filters.DateTo, out var to);
        if (from == default) from = DateTime.UtcNow.AddDays(-30);
        if (to == default) to = DateTime.UtcNow;
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);

        var visits = await _uow.SchoolVisitLogs.Query()
            .Include(v => v.School)
            .Where(v => v.VisitDate >= fromUtc && v.VisitDate < toUtc)
            .ToListAsync();

        return visits.GroupBy(v => v.SchoolId).Select(g =>
        {
            var school = g.First().School;
            var completedVisits = g.Where(v => v.DurationMinutes.HasValue).ToList();
            return new SchoolVisitSummaryDto
            {
                SchoolId = g.Key, SchoolName = school?.Name ?? "", City = school?.City,
                TotalVisits = g.Count(),
                TotalTimeSpentMinutes = completedVisits.Sum(v => v.DurationMinutes!.Value),
                AvgVisitDurationMinutes = completedVisits.Any() ? (decimal)completedVisits.Average(v => (double)v.DurationMinutes!.Value) : 0,
                UniqueVisitors = g.Select(v => v.UserId).Distinct().Count(),
                LastVisitDate = g.Max(v => v.EnteredAt)
            };
        }).OrderByDescending(s => s.TotalVisits).ToList();
    }

    public async Task<List<PipelineReportDto>> GetPipelineReportAsync(ReportFilters filters)
    {
        var leads = await _uow.Leads.Query().ToListAsync();

        return leads.GroupBy(l => l.Stage.ToString()).Select(g => new PipelineReportDto
        {
            Stage = g.Key,
            Count = g.Count(),
            TotalValue = g.Sum(l => l.Value),
            AvgAgeDays = g.Any() ? (decimal)g.Average(l => (DateTime.UtcNow - l.CreatedAt).TotalDays) : 0
        }).ToList();
    }
}
