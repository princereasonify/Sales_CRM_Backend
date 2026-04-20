using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Leave;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class LeaveService : ILeaveService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notify;

    public LeaveService(IUnitOfWork uow, INotificationService notify)
    {
        _uow = uow;
        _notify = notify;
    }

    public async Task<LeaveRequestDto> ApplyLeaveAsync(ApplyLeaveRequest request, int userId)
    {
        var user = await _uow.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new Exception("User not found");

        if (!Enum.TryParse<LeaveType>(request.LeaveType, true, out var leaveType))
            throw new Exception("Invalid leave type");
        if (!Enum.TryParse<LeaveCategory>(request.LeaveCategory, true, out var leaveCategory))
            throw new Exception("Invalid leave category");

        var leaveDate = DateTime.SpecifyKind(request.LeaveDate.Date, DateTimeKind.Utc);
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        if (leaveDate < today)
            throw new Exception("Cannot apply leave for past dates");

        // Check for duplicate leave on same date
        var existing = await _uow.LeaveRequests.Query()
            .AnyAsync(l => l.UserId == userId && l.LeaveDate == leaveDate
                && l.Status != LeaveStatus.Rejected && l.Status != LeaveStatus.Cancelled);
        if (existing)
            throw new Exception("You already have a leave request for this date");

        var isSameDay = leaveDate == today;

        var leave = new LeaveRequest
        {
            UserId = userId,
            LeaveDate = leaveDate,
            LeaveType = leaveType,
            LeaveCategory = leaveCategory,
            Reason = request.Reason,
            CoverArrangement = request.CoverArrangement,
            IsSameDay = isSameDay,
        };

        // Same-day leave or SH: auto-approve
        if (isSameDay || user.Role == UserRole.SH)
        {
            leave.Status = LeaveStatus.AutoApproved;
            leave.ActionedAt = DateTime.UtcNow;
        }
        else
        {
            leave.Status = LeaveStatus.Pending;
        }

        await _uow.LeaveRequests.AddAsync(leave);
        await _uow.SaveChangesAsync();

        // Build plan impact message
        var planImpact = await GetPlanImpactAsync(userId, leaveDate);

        // Notifications
        var superiorId = await GetSuperiorIdAsync(user);

        if (leave.Status == LeaveStatus.AutoApproved)
        {
            // FYI notification to superior
            if (superiorId.HasValue)
            {
                var msg = $"{user.Name} has taken a same-day leave today ({leaveDate:dd MMM yyyy}). Category: {leaveCategory}. Reason: {request.Reason}.";
                if (!string.IsNullOrEmpty(planImpact))
                    msg += $"\n{planImpact}";
                await _notify.CreateNotificationAsync(superiorId.Value, NotificationType.Info, "Same-Day Leave Taken", msg);
            }
        }
        else
        {
            // Approval request to superior
            if (superiorId.HasValue)
            {
                var msg = $"{user.Name} has applied for {leaveCategory} leave on {leaveDate:dd MMM yyyy} ({leaveType}). Reason: {request.Reason}.";
                if (!string.IsNullOrEmpty(planImpact))
                    msg += $"\n{planImpact}";
                await _notify.CreateNotificationAsync(superiorId.Value, NotificationType.Urgent, "Leave Approval Required", msg);
            }
        }

        return await GetLeaveByIdAsync(leave.Id) ?? throw new Exception("Failed to create leave");
    }

    public async Task<List<LeaveRequestDto>> GetMyLeavesAsync(int userId, string? status, string? category, DateTime? from, DateTime? to)
    {
        var query = _uow.LeaveRequests.Query()
            .Include(l => l.User).Include(l => l.ActionedBy)
            .Where(l => l.UserId == userId);

        query = ApplyFilters(query, status, category, from, to);

        return await query.OrderByDescending(l => l.LeaveDate)
            .Select(l => ToDto(l))
            .ToListAsync();
    }

    public async Task<List<LeaveRequestDto>> GetTeamLeavesAsync(int managerId, string role, string? status, string? category, DateTime? from, DateTime? to, int? filterUserId = null)
    {
        var manager = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == managerId);
        if (manager == null) return new();

        var query = _uow.LeaveRequests.Query()
            .Include(l => l.User).Include(l => l.ActionedBy)
            .AsQueryable();

        // Scope by role hierarchy — show immediate subordinates
        query = role switch
        {
            "ZH" => query.Where(l => l.User.Role == UserRole.FO && l.User.ZoneId == manager.ZoneId),
            "RH" => query.Where(l => l.User.Role == UserRole.ZH && l.User.RegionId == manager.RegionId),
            "SH" => query.Where(l => l.User.Role == UserRole.RH),
            "SCA" => query,
            _ => query.Where(l => false)
        };

        if (filterUserId.HasValue)
            query = query.Where(l => l.UserId == filterUserId.Value);

        query = ApplyFilters(query, status, category, from, to);

        return await query.OrderByDescending(l => l.LeaveDate)
            .Select(l => ToDto(l))
            .ToListAsync();
    }

    public async Task<LeaveRequestDto?> ApproveLeaveAsync(int leaveId, int approverId)
    {
        var leave = await _uow.LeaveRequests.Query()
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == leaveId);
        if (leave == null || leave.Status != LeaveStatus.Pending) return null;

        // Prevent self-approval
        if (leave.UserId == approverId)
            throw new UnauthorizedAccessException("You cannot approve your own leave request");

        leave.Status = LeaveStatus.Approved;
        leave.ActionedById = approverId;
        leave.ActionedAt = DateTime.UtcNow;
        await _uow.LeaveRequests.UpdateAsync(leave);
        await _uow.SaveChangesAsync();

        var approver = await _uow.Users.GetByIdAsync(approverId);
        var planImpact = await GetPlanImpactAsync(leave.UserId, leave.LeaveDate);

        // Notify the applicant
        var applicantMsg = $"Your leave on {leave.LeaveDate:dd MMM yyyy} has been approved by {approver?.Name}.";
        if (!string.IsNullOrEmpty(planImpact))
            applicantMsg += $"\n{planImpact}";
        await _notify.CreateNotificationAsync(leave.UserId, NotificationType.Success, "Leave Approved", applicantMsg);

        // Notify the approver about plan impact
        if (!string.IsNullOrEmpty(planImpact))
        {
            var superiorMsg = $"You have approved leave for {leave.User.Name} on {leave.LeaveDate:dd MMM yyyy}.\n{planImpact}\nPlease ensure coverage is arranged.";
            await _notify.CreateNotificationAsync(approverId, NotificationType.Warning, "Leave Plan Impact", superiorMsg);
        }

        return await GetLeaveByIdAsync(leaveId);
    }

    public async Task<LeaveRequestDto?> RejectLeaveAsync(int leaveId, RejectLeaveRequest request, int approverId)
    {
        var leave = await _uow.LeaveRequests.Query()
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == leaveId);
        if (leave == null || leave.Status != LeaveStatus.Pending) return null;

        // Prevent self-rejection (same principle — you can only cancel your own, not reject it)
        if (leave.UserId == approverId)
            throw new UnauthorizedAccessException("You cannot reject your own leave request (use Cancel instead)");

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
            throw new InvalidOperationException("Rejection reason is required");

        leave.Status = LeaveStatus.Rejected;
        leave.ActionedById = approverId;
        leave.ActionedAt = DateTime.UtcNow;
        leave.RejectionReason = request.RejectionReason;
        await _uow.LeaveRequests.UpdateAsync(leave);
        await _uow.SaveChangesAsync();

        var approver = await _uow.Users.GetByIdAsync(approverId);

        // Notify the applicant
        var msg = $"Your leave on {leave.LeaveDate:dd MMM yyyy} has been rejected by {approver?.Name}. Reason: {request.RejectionReason}";
        await _notify.CreateNotificationAsync(leave.UserId, NotificationType.Warning, "Leave Rejected", msg);

        return await GetLeaveByIdAsync(leaveId);
    }

    public async Task<LeaveRequestDto?> CancelLeaveAsync(int leaveId, int userId)
    {
        var leave = await _uow.LeaveRequests.Query()
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == leaveId);
        if (leave == null || leave.UserId != userId) return null;
        if (leave.Status == LeaveStatus.Rejected || leave.Status == LeaveStatus.Cancelled) return null;

        leave.Status = LeaveStatus.Cancelled;
        await _uow.LeaveRequests.UpdateAsync(leave);
        await _uow.SaveChangesAsync();

        // Notify superior about cancellation
        var superiorId = await GetSuperiorIdAsync(leave.User);
        if (superiorId.HasValue)
        {
            await _notify.CreateNotificationAsync(superiorId.Value, NotificationType.Info,
                "Leave Cancelled", $"{leave.User.Name} has cancelled their leave on {leave.LeaveDate:dd MMM yyyy}.");
        }

        return await GetLeaveByIdAsync(leaveId);
    }

    // --- Helpers ---

    private async Task<LeaveRequestDto?> GetLeaveByIdAsync(int id)
    {
        var leave = await _uow.LeaveRequests.Query()
            .Include(l => l.User).Include(l => l.ActionedBy)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (leave == null) return null;

        var dto = ToDto(leave);
        dto.PlanImpactMessage = await GetPlanImpactAsync(leave.UserId, leave.LeaveDate);
        return dto;
    }

    private async Task<int?> GetSuperiorIdAsync(User user)
    {
        return user.Role switch
        {
            UserRole.FO => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == user.ZoneId))?.Id,
            UserRole.ZH => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.RH && u.RegionId == user.RegionId))?.Id,
            UserRole.RH => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.SH))?.Id,
            _ => null
        };
    }

    private async Task<string?> GetPlanImpactAsync(int userId, DateTime leaveDate)
    {
        var impacts = new List<string>();

        // Check weekly plan
        var weekStart = NormalizeWeekStart(leaveDate);
        var weeklyPlan = await _uow.WeeklyPlans.Query()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WeekStartDate == weekStart
                && (w.Status == WeeklyPlanStatus.Approved || w.Status == WeeklyPlanStatus.Submitted || w.Status == WeeklyPlanStatus.PendingReApproval));

        if (weeklyPlan != null && !string.IsNullOrEmpty(weeklyPlan.PlanData))
        {
            try
            {
                var days = JsonSerializer.Deserialize<List<PlanDay>>(weeklyPlan.PlanData,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var dayPlan = days?.FirstOrDefault(d =>
                    DateTime.TryParse(d.Date, out var dt) && dt.Date == leaveDate.Date);

                if (dayPlan?.Activities != null && dayPlan.Activities.Count > 0)
                {
                    var activitySummaries = dayPlan.Activities
                        .Select(a => $"{a.Type} at {a.SchoolName}")
                        .ToList();
                    impacts.Add($"Weekly plan has {dayPlan.Activities.Count} activities: {string.Join(", ", activitySummaries)}");
                }
            }
            catch { /* ignore malformed JSON */ }
        }

        // Check school assignments
        var assignments = await _uow.SchoolAssignments.Query()
            .Include(a => a.School)
            .Where(a => a.UserId == userId && a.AssignmentDate.Date == leaveDate.Date && !a.IsVisited)
            .ToListAsync();

        if (assignments.Count > 0)
        {
            var schoolNames = assignments.Select(a => a.School.Name).ToList();
            impacts.Add($"{assignments.Count} school assignments: {string.Join(", ", schoolNames)}");
        }

        // Check demo assignments
        var demos = await _uow.DemoAssignments.Query()
            .Include(d => d.School)
            .Where(d => d.AssignedToId == userId && d.ScheduledDate.Date == leaveDate.Date
                && d.Status != DemoStatus.Completed && d.Status != DemoStatus.Cancelled)
            .ToListAsync();

        if (demos.Count > 0)
        {
            var demoDetails = demos.Select(d => $"Demo at {d.School.Name}").ToList();
            impacts.Add($"{demos.Count} demos: {string.Join(", ", demoDetails)}");
        }

        if (impacts.Count == 0) return null;

        return $"This leave affects {impacts.Count} planned activities on {leaveDate:dd MMM yyyy}: {string.Join("; ", impacts)}. Please plan accordingly.";
    }

    private static DateTime NormalizeWeekStart(DateTime date)
    {
        var d = date.Date;
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return DateTime.SpecifyKind(d.AddDays(-diff), DateTimeKind.Utc);
    }

    private static IQueryable<LeaveRequest> ApplyFilters(IQueryable<LeaveRequest> query, string? status, string? category, DateTime? from, DateTime? to)
    {
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<LeaveStatus>(status, true, out var s))
            query = query.Where(l => l.Status == s);

        if (!string.IsNullOrEmpty(category) && Enum.TryParse<LeaveCategory>(category, true, out var c))
            query = query.Where(l => l.LeaveCategory == c);

        if (from.HasValue)
            query = query.Where(l => l.LeaveDate >= DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc));

        if (to.HasValue)
            query = query.Where(l => l.LeaveDate <= DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc));

        return query;
    }

    private static LeaveRequestDto ToDto(LeaveRequest l) => new()
    {
        Id = l.Id,
        UserId = l.UserId,
        UserName = l.User?.Name ?? "",
        UserRole = l.User?.Role.ToString() ?? "",
        LeaveDate = l.LeaveDate,
        LeaveType = l.LeaveType.ToString(),
        LeaveCategory = l.LeaveCategory.ToString(),
        Status = l.Status.ToString(),
        Reason = l.Reason,
        CoverArrangement = l.CoverArrangement,
        IsSameDay = l.IsSameDay,
        ActionedById = l.ActionedById,
        ActionedByName = l.ActionedBy?.Name,
        ActionedAt = l.ActionedAt,
        RejectionReason = l.RejectionReason,
        CreatedAt = l.CreatedAt
    };

    // Internal classes for parsing weekly plan JSON
    private class PlanDay
    {
        public string Date { get; set; } = "";
        public string DayOfWeek { get; set; } = "";
        public List<PlanActivity> Activities { get; set; } = new();
    }

    private class PlanActivity
    {
        public string Type { get; set; } = "";
        public string SchoolId { get; set; } = "";
        public string SchoolName { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}
