using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.WeeklyPlan;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class WeeklyPlanService : IWeeklyPlanService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notify;

    public WeeklyPlanService(IUnitOfWork uow, INotificationService notify)
    {
        _uow = uow;
        _notify = notify;
    }

    public async Task<WeeklyPlanDto?> GetMyPlanAsync(int userId, DateTime weekStart)
    {
        var ws = NormalizeWeekStart(weekStart);
        var plan = await _uow.WeeklyPlans.Query()
            .Include(w => w.User)
            .Include(w => w.ReviewedBy)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WeekStartDate == ws);
        return plan == null ? null : ToDto(plan);
    }

    public async Task<List<WeeklyPlanDto>> GetTeamPlansAsync(int managerId, string role, DateTime weekStart)
    {
        var ws = NormalizeWeekStart(weekStart);
        var manager = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == managerId);
        if (manager == null) return new();

        var query = _uow.WeeklyPlans.Query()
            .Include(w => w.User)
            .Include(w => w.ReviewedBy)
            .Where(w => w.WeekStartDate == ws && w.Status != WeeklyPlanStatus.Draft);

        // Scope: show subordinates' plans
        query = role switch
        {
            "ZH" => query.Where(w => w.User.Role == UserRole.FO && w.User.ZoneId == manager.ZoneId),
            "RH" => query.Where(w => w.User.Role == UserRole.ZH && w.User.RegionId == manager.RegionId),
            "SH" => query.Where(w => w.User.Role == UserRole.RH),
            "SCA" => query, // see all
            _ => query.Where(w => false)
        };

        return await query.OrderBy(w => w.User.Name).Select(w => ToDto(w)).ToListAsync();
    }

    public async Task<WeeklyPlanDto> CreatePlanAsync(CreateWeeklyPlanRequest request, int userId)
    {
        var ws = NormalizeWeekStart(request.WeekStartDate);
        var we = ws.AddDays(6);

        // Check if plan already exists for this week
        var existing = await _uow.WeeklyPlans.Query()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WeekStartDate == ws);
        if (existing != null)
        {
            existing.PlanData = request.PlanData;
            existing.Status = WeeklyPlanStatus.Draft;
            await _uow.WeeklyPlans.UpdateAsync(existing);
            await _uow.SaveChangesAsync();
            return (await GetMyPlanAsync(userId, ws))!;
        }

        var plan = new WeeklyPlan
        {
            UserId = userId,
            WeekStartDate = ws,
            WeekEndDate = we,
            PlanData = request.PlanData,
            Status = WeeklyPlanStatus.Draft
        };
        await _uow.WeeklyPlans.AddAsync(plan);
        await _uow.SaveChangesAsync();
        return (await GetMyPlanAsync(userId, ws))!;
    }

    public async Task<WeeklyPlanDto?> UpdatePlanAsync(int id, UpdateWeeklyPlanRequest request, int userId)
    {
        var plan = await _uow.WeeklyPlans.GetByIdAsync(id);
        if (plan == null || plan.UserId != userId) return null;

        plan.PlanData = request.PlanData;
        if (plan.Status == WeeklyPlanStatus.Rejected || plan.Status == WeeklyPlanStatus.EditedByManager)
            plan.Status = WeeklyPlanStatus.Draft;

        await _uow.WeeklyPlans.UpdateAsync(plan);
        await _uow.SaveChangesAsync();
        return await GetPlanById(id);
    }

    public async Task<WeeklyPlanDto?> SubmitPlanAsync(int id, int userId)
    {
        var plan = await _uow.WeeklyPlans.Query()
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (plan == null || plan.UserId != userId) return null;

        plan.Status = WeeklyPlanStatus.Submitted;
        plan.SubmittedAt = DateTime.UtcNow;
        await _uow.WeeklyPlans.UpdateAsync(plan);
        await _uow.SaveChangesAsync();

        // Notify the reviewer
        var reviewerId = await GetReviewerIdAsync(plan.User);
        if (reviewerId.HasValue)
        {
            await _notify.CreateNotificationAsync(reviewerId.Value, NotificationType.Info,
                "Weekly Plan Submitted", $"{plan.User.Name} submitted their weekly plan for review ({plan.WeekStartDate:dd MMM} - {plan.WeekEndDate:dd MMM})");
        }

        return await GetPlanById(id);
    }

    public async Task<WeeklyPlanDto?> ApprovePlanAsync(int id, int reviewerId)
    {
        var plan = await _uow.WeeklyPlans.Query().Include(w => w.User).FirstOrDefaultAsync(w => w.Id == id);
        if (plan == null) return null;

        plan.Status = WeeklyPlanStatus.Approved;
        plan.ReviewedById = reviewerId;
        plan.ReviewedAt = DateTime.UtcNow;
        await _uow.WeeklyPlans.UpdateAsync(plan);
        await _uow.SaveChangesAsync();

        var reviewer = await _uow.Users.GetByIdAsync(reviewerId);
        await _notify.CreateNotificationAsync(plan.UserId, NotificationType.Success,
            "Weekly Plan Approved", $"Your weekly plan ({plan.WeekStartDate:dd MMM} - {plan.WeekEndDate:dd MMM}) was approved by {reviewer?.Name}");

        return await GetPlanById(id);
    }

    public async Task<WeeklyPlanDto?> EditPlanAsync(int id, ManagerEditRequest request, int reviewerId)
    {
        var plan = await _uow.WeeklyPlans.Query().Include(w => w.User).FirstOrDefaultAsync(w => w.Id == id);
        if (plan == null) return null;

        plan.Status = WeeklyPlanStatus.EditedByManager;
        plan.ManagerEdits = request.ManagerEdits;
        plan.ReviewedById = reviewerId;
        plan.ReviewedAt = DateTime.UtcNow;
        plan.ReviewNotes = request.ReviewNotes;
        await _uow.WeeklyPlans.UpdateAsync(plan);
        await _uow.SaveChangesAsync();

        var reviewer = await _uow.Users.GetByIdAsync(reviewerId);
        await _notify.CreateNotificationAsync(plan.UserId, NotificationType.Warning,
            "Weekly Plan Edited by Manager", $"Your weekly plan ({plan.WeekStartDate:dd MMM} - {plan.WeekEndDate:dd MMM}) was edited by {reviewer?.Name}. Please review the changes.");

        return await GetPlanById(id);
    }

    public async Task<WeeklyPlanDto?> RejectPlanAsync(int id, RejectPlanRequest request, int reviewerId)
    {
        var plan = await _uow.WeeklyPlans.Query().Include(w => w.User).FirstOrDefaultAsync(w => w.Id == id);
        if (plan == null) return null;

        plan.Status = WeeklyPlanStatus.Rejected;
        plan.ReviewedById = reviewerId;
        plan.ReviewedAt = DateTime.UtcNow;
        plan.ReviewNotes = request.ReviewNotes;
        await _uow.WeeklyPlans.UpdateAsync(plan);
        await _uow.SaveChangesAsync();

        var reviewer = await _uow.Users.GetByIdAsync(reviewerId);
        await _notify.CreateNotificationAsync(plan.UserId, NotificationType.Warning,
            "Weekly Plan Rejected", $"Your weekly plan ({plan.WeekStartDate:dd MMM} - {plan.WeekEndDate:dd MMM}) was rejected by {reviewer?.Name}. Reason: {request.ReviewNotes ?? "No reason"}");

        return await GetPlanById(id);
    }

    // --- Helpers ---

    private async Task<WeeklyPlanDto?> GetPlanById(int id)
    {
        var plan = await _uow.WeeklyPlans.Query()
            .Include(w => w.User).Include(w => w.ReviewedBy)
            .FirstOrDefaultAsync(w => w.Id == id);
        return plan == null ? null : ToDto(plan);
    }

    private async Task<int?> GetReviewerIdAsync(User user)
    {
        return user.Role switch
        {
            UserRole.FO => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == user.ZoneId))?.Id,
            UserRole.ZH => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.RH && u.RegionId == user.RegionId))?.Id,
            UserRole.RH => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.SH))?.Id,
            UserRole.SH => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.SCA))?.Id,
            _ => null
        };
    }

    private static DateTime NormalizeWeekStart(DateTime date)
    {
        var d = date.Date;
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return DateTime.SpecifyKind(d.AddDays(-diff), DateTimeKind.Utc);
    }

    private static WeeklyPlanDto ToDto(WeeklyPlan w) => new()
    {
        Id = w.Id,
        UserId = w.UserId,
        UserName = w.User?.Name ?? "",
        UserRole = w.User?.Role.ToString() ?? "",
        WeekStartDate = w.WeekStartDate,
        WeekEndDate = w.WeekEndDate,
        PlanData = w.PlanData,
        Status = w.Status.ToString(),
        SubmittedAt = w.SubmittedAt,
        ReviewedById = w.ReviewedById,
        ReviewedByName = w.ReviewedBy?.Name,
        ReviewedAt = w.ReviewedAt,
        ReviewNotes = w.ReviewNotes,
        ManagerEdits = w.ManagerEdits,
        CreatedAt = w.CreatedAt
    };
}
