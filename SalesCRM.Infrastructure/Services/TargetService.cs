using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Target;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class TargetService : ITargetService
{
    private readonly IUnitOfWork _unitOfWork;

    public TargetService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // Strict one-level-down only
    private static string? GetDirectSubordinateRole(string role) => role switch
    {
        "SH" => "RH",
        "RH" => "ZH",
        "ZH" => "FO",
        _ => null
    };

    public async Task<TargetAssignmentDto> CreateTargetAsync(CreateTargetRequest request, int assignedById)
    {
        var assigner = await _unitOfWork.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == assignedById)
            ?? throw new InvalidOperationException("Assigner not found");

        var assignedTo = await _unitOfWork.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == request.AssignedToId)
            ?? throw new InvalidOperationException("Assigned user not found");

        // Strict hierarchy: only assign one level down
        var allowedRole = GetDirectSubordinateRole(assigner.Role.ToString());
        if (allowedRole == null || assignedTo.Role.ToString() != allowedRole)
            throw new InvalidOperationException(
                $"{assigner.Role} can only assign targets to {allowedRole ?? "nobody"}");

        // Validate sub-target doesn't exceed parent remaining
        if (request.ParentTargetId != null)
        {
            var parent = await _unitOfWork.TargetAssignments.Query()
                .Include(t => t.SubTargets)
                .FirstOrDefaultAsync(t => t.Id == request.ParentTargetId)
                ?? throw new InvalidOperationException("Parent target not found");

            var existingAmountTotal = parent.SubTargets.Sum(s => s.TargetAmount);
            var existingSchoolsTotal = parent.SubTargets.Sum(s => s.NumberOfSchools);

            if (existingAmountTotal + request.TargetAmount > parent.TargetAmount)
                throw new InvalidOperationException(
                    $"Amount total would exceed parent target. Remaining: ₹{parent.TargetAmount - existingAmountTotal:N0}");

            if (existingSchoolsTotal + request.NumberOfSchools > parent.NumberOfSchools)
                throw new InvalidOperationException(
                    $"Schools total would exceed parent target. Remaining: {parent.NumberOfSchools - existingSchoolsTotal}");

            if (request.NumberOfLogins.HasValue && parent.NumberOfLogins.HasValue)
            {
                var existingLoginsTotal = parent.SubTargets.Sum(s => s.NumberOfLogins ?? 0);
                if (existingLoginsTotal + request.NumberOfLogins.Value > parent.NumberOfLogins.Value)
                    throw new InvalidOperationException(
                        $"Logins total would exceed parent target. Remaining: {parent.NumberOfLogins.Value - existingLoginsTotal}");
            }

            if (request.NumberOfStudents.HasValue && parent.NumberOfStudents.HasValue)
            {
                var existingStudentsTotal = parent.SubTargets.Sum(s => s.NumberOfStudents ?? 0);
                if (existingStudentsTotal + request.NumberOfStudents.Value > parent.NumberOfStudents.Value)
                    throw new InvalidOperationException(
                        $"Students total would exceed parent target. Remaining: {parent.NumberOfStudents.Value - existingStudentsTotal}");
            }
        }

        var periodType = Enum.Parse<PeriodType>(request.PeriodType, true);

        var target = new TargetAssignment
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            TargetAmount = request.TargetAmount,
            AchievedAmount = 0,
            NumberOfSchools = request.NumberOfSchools,
            AchievedSchools = 0,
            NumberOfLogins = request.NumberOfLogins,
            AchievedLogins = request.NumberOfLogins.HasValue ? 0 : null,
            NumberOfStudents = request.NumberOfStudents,
            AchievedStudents = request.NumberOfStudents.HasValue ? 0 : null,
            PeriodType = periodType,
            StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc),
            Status = TargetStatus.Pending,
            AssignedToId = request.AssignedToId,
            AssignedById = assignedById,
            ParentTargetId = request.ParentTargetId,
        };

        await _unitOfWork.TargetAssignments.AddAsync(target);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(target, assigner, assignedTo);
    }

    public async Task<List<TargetAssignmentDto>> GetMyTargetsAsync(int userId)
    {
        var targets = await BaseQuery()
            .Where(t => t.AssignedToId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return targets.Select(t => MapToDto(t)).ToList();
    }

    public async Task<List<TargetAssignmentDto>> GetAssignedByMeAsync(int userId)
    {
        var targets = await BaseQuery()
            .Where(t => t.AssignedById == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return targets.Select(t => MapToDto(t)).ToList();
    }

    public async Task<List<TargetAssignmentDto>> GetSubTargetsAsync(int parentTargetId)
    {
        var targets = await BaseQuery()
            .Where(t => t.ParentTargetId == parentTargetId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return targets.Select(t => MapToDto(t)).ToList();
    }

    // Assignee updates their own progress (amount/schools achieved)
    public async Task<TargetAssignmentDto> UpdateProgressAsync(int targetId, UpdateTargetRequest request, int userId)
    {
        var target = await BaseQuery().FirstOrDefaultAsync(t => t.Id == targetId)
            ?? throw new InvalidOperationException("Target not found");

        if (target.AssignedToId != userId)
            throw new InvalidOperationException("Only the assignee can update progress");

        if (target.Status == TargetStatus.Approved)
            throw new InvalidOperationException("Cannot update an approved target");

        target.AchievedAmount = request.AchievedAmount;
        target.AchievedSchools = request.AchievedSchools;
        if (request.AchievedLogins.HasValue)
            target.AchievedLogins = request.AchievedLogins.Value;
        if (request.AchievedStudents.HasValue)
            target.AchievedStudents = request.AchievedStudents.Value;

        if (target.Status == TargetStatus.Pending)
            target.Status = TargetStatus.InProgress;

        // If rejected, allow re-working → set back to InProgress
        if (target.Status == TargetStatus.Rejected)
            target.Status = TargetStatus.InProgress;

        await _unitOfWork.TargetAssignments.UpdateAsync(target);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(target);
    }

    // Assignee submits target for review by assigner
    public async Task<TargetAssignmentDto> SubmitTargetAsync(int targetId, int userId)
    {
        var target = await BaseQuery().FirstOrDefaultAsync(t => t.Id == targetId)
            ?? throw new InvalidOperationException("Target not found");

        if (target.AssignedToId != userId)
            throw new InvalidOperationException("Only the assignee can submit for review");

        if (target.Status == TargetStatus.Approved)
            throw new InvalidOperationException("Target is already approved");

        if (target.Status == TargetStatus.Submitted)
            throw new InvalidOperationException("Target is already submitted for review");

        target.Status = TargetStatus.Submitted;
        target.SubmittedAt = DateTime.UtcNow;

        await _unitOfWork.TargetAssignments.UpdateAsync(target);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(target);
    }

    // Assigner reviews a submitted target (approve/reject)
    public async Task<TargetAssignmentDto> ReviewTargetAsync(int targetId, ReviewTargetRequest request, int userId)
    {
        var target = await BaseQuery().FirstOrDefaultAsync(t => t.Id == targetId)
            ?? throw new InvalidOperationException("Target not found");

        if (target.AssignedById != userId)
            throw new InvalidOperationException("Only the assigner can review this target");

        if (target.Status != TargetStatus.Submitted)
            throw new InvalidOperationException("Target must be in Submitted status to review");

        target.ReviewedAt = DateTime.UtcNow;
        target.ReviewNote = request.Note?.Trim();

        if (request.Approved)
        {
            target.Status = TargetStatus.Approved;

            // Auto-propagate: if ALL sub-targets of the parent are approved,
            // update parent's achieved totals from sub-targets
            if (target.ParentTargetId != null)
            {
                await UpdateParentAchievedAsync(target.ParentTargetId.Value);
            }
        }
        else
        {
            target.Status = TargetStatus.Rejected;
            target.SubmittedAt = null; // allow re-submit
        }

        await _unitOfWork.TargetAssignments.UpdateAsync(target);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(target);
    }

    // When a sub-target is approved, roll up achieved totals to parent
    private async Task UpdateParentAchievedAsync(int parentId)
    {
        var parent = await _unitOfWork.TargetAssignments.Query()
            .Include(t => t.SubTargets)
            .FirstOrDefaultAsync(t => t.Id == parentId);

        if (parent == null) return;

        parent.AchievedAmount = parent.SubTargets.Sum(s => s.AchievedAmount);
        parent.AchievedSchools = parent.SubTargets.Sum(s => s.AchievedSchools);
        if (parent.NumberOfLogins.HasValue)
            parent.AchievedLogins = parent.SubTargets.Sum(s => s.AchievedLogins ?? 0);
        if (parent.NumberOfStudents.HasValue)
            parent.AchievedStudents = parent.SubTargets.Sum(s => s.AchievedStudents ?? 0);

        await _unitOfWork.TargetAssignments.UpdateAsync(parent);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteTargetAsync(int targetId, int userId)
    {
        var target = await _unitOfWork.TargetAssignments.Query()
            .Include(t => t.SubTargets)
            .FirstOrDefaultAsync(t => t.Id == targetId)
            ?? throw new InvalidOperationException("Target not found");

        if (target.AssignedById != userId)
            throw new InvalidOperationException("Only the assigner can delete this target");

        if (target.SubTargets.Any())
            throw new InvalidOperationException("Cannot delete a target that has sub-targets. Delete sub-targets first.");

        await _unitOfWork.TargetAssignments.DeleteAsync(target);
        await _unitOfWork.SaveChangesAsync();
    }

    // Only return direct subordinate role users
    public async Task<List<UserDto>> GetAssignableUsersAsync(int userId, string userRole)
    {
        var subRole = GetDirectSubordinateRole(userRole);
        if (subRole == null) return new List<UserDto>();

        var targetRole = Enum.Parse<UserRole>(subRole);
        var query = _unitOfWork.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .Where(u => u.Role == targetRole);

        // RH sees only ZHs in their region
        if (userRole == "RH")
        {
            var me = await _unitOfWork.Users.GetByIdAsync(userId);
            if (me?.RegionId != null)
                query = query.Where(u => u.RegionId == me.RegionId);
        }
        // ZH sees only FOs in their zone
        else if (userRole == "ZH")
        {
            var me = await _unitOfWork.Users.GetByIdAsync(userId);
            if (me?.ZoneId != null)
                query = query.Where(u => u.ZoneId == me.ZoneId);
        }

        var users = await query.OrderBy(u => u.Name).ToListAsync();
        return users.Select(u => new UserDto
        {
            Id = u.Id, Name = u.Name, Email = u.Email,
            Role = u.Role.ToString(), Avatar = u.Avatar,
            ZoneId = u.ZoneId, Zone = u.Zone?.Name,
            RegionId = u.RegionId, Region = u.Region?.Name,
        }).ToList();
    }

    // Get full hierarchy tree for SH view: target → sub-targets → sub-sub-targets → FO level
    public async Task<List<TargetAssignmentDto>> GetFullHierarchyAsync(int targetId)
    {
        var all = new List<TargetAssignment>();
        await LoadDescendants(targetId, all);
        return all.Select(t => MapToDto(t)).ToList();
    }

    private async Task LoadDescendants(int parentId, List<TargetAssignment> results)
    {
        var children = await BaseQuery()
            .Where(t => t.ParentTargetId == parentId)
            .ToListAsync();
        results.AddRange(children);
        foreach (var child in children)
            await LoadDescendants(child.Id, results);
    }

    private IQueryable<TargetAssignment> BaseQuery() =>
        _unitOfWork.TargetAssignments.Query()
            .Include(t => t.AssignedTo).ThenInclude(u => u.Zone)
            .Include(t => t.AssignedTo).ThenInclude(u => u.Region)
            .Include(t => t.AssignedBy)
            .Include(t => t.SubTargets);

    private static TargetAssignmentDto MapToDto(TargetAssignment t, User? assigner = null, User? assignedTo = null)
    {
        var to = assignedTo ?? t.AssignedTo;
        var by = assigner ?? t.AssignedBy;
        return new TargetAssignmentDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            TargetAmount = t.TargetAmount,
            AchievedAmount = t.AchievedAmount,
            NumberOfSchools = t.NumberOfSchools,
            AchievedSchools = t.AchievedSchools,
            NumberOfLogins = t.NumberOfLogins,
            AchievedLogins = t.AchievedLogins,
            NumberOfStudents = t.NumberOfStudents,
            AchievedStudents = t.AchievedStudents,
            PeriodType = t.PeriodType.ToString(),
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Status = t.Status.ToString(),
            AssignedToId = t.AssignedToId,
            AssignedToName = to?.Name ?? "",
            AssignedToRole = to?.Role.ToString() ?? "",
            AssignedToZone = to?.Zone?.Name,
            AssignedToRegion = to?.Region?.Name,
            AssignedById = t.AssignedById,
            AssignedByName = by?.Name ?? "",
            AssignedByRole = by?.Role.ToString() ?? "",
            ParentTargetId = t.ParentTargetId,
            SubTargetTotal = t.SubTargets?.Sum(s => s.TargetAmount) ?? 0,
            SubTargetSchoolsTotal = t.SubTargets?.Sum(s => s.NumberOfSchools) ?? 0,
            SubTargetLoginsTotal = t.SubTargets?.Sum(s => s.NumberOfLogins ?? 0) ?? 0,
            SubTargetStudentsTotal = t.SubTargets?.Sum(s => s.NumberOfStudents ?? 0) ?? 0,
            SubTargetCount = t.SubTargets?.Count ?? 0,
            SubmittedAt = t.SubmittedAt,
            ReviewedAt = t.ReviewedAt,
            ReviewNote = t.ReviewNote,
            CreatedAt = t.CreatedAt,
        };
    }
}
