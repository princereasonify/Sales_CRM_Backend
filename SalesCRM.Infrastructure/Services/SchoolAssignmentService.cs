using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.SchoolAssignment;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class SchoolAssignmentService : ISchoolAssignmentService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;

    public SchoolAssignmentService(IUnitOfWork uow, INotificationService notificationService)
    {
        _uow = uow;
        _notificationService = notificationService;
    }

    private static DateTime GetTodayIst()
    {
        var ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist);
    }

    public async Task<List<SchoolAssignmentDto>> BulkAssignAsync(int assignedById, BulkAssignRequest request)
    {
        if (!DateTime.TryParse(request.AssignmentDate, out var date))
            throw new ArgumentException("Invalid date format");

        var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // Wholesale-replace the target user's plan for this date
        var existing = await _uow.SchoolAssignments.Query()
            .Where(a => a.UserId == request.UserId && a.AssignmentDate.Date == dateUtc.Date)
            .ToListAsync();
        foreach (var e in existing)
            await _uow.SchoolAssignments.DeleteAsync(e);

        // Also drop any prior owner of these same (school, date) pairs — when a
        // manager forwards a school down the hierarchy (RH → ZH/FO, ZH → FO), the
        // original owner must lose it. Without this, the school would sit in both
        // owners' plans for the same day.
        if (request.SchoolIds.Count > 0)
        {
            var crossUserPrior = await _uow.SchoolAssignments.Query()
                .Where(a => request.SchoolIds.Contains(a.SchoolId)
                            && a.AssignmentDate.Date == dateUtc.Date
                            && a.UserId != request.UserId)
                .ToListAsync();
            foreach (var p in crossUserPrior)
                await _uow.SchoolAssignments.DeleteAsync(p);
        }

        // Create new assignments with visit order
        var assignments = new List<SchoolAssignment>();
        for (int i = 0; i < request.SchoolIds.Count; i++)
        {
            var a = new SchoolAssignment
            {
                SchoolId = request.SchoolIds[i],
                UserId = request.UserId,
                AssignedById = assignedById,
                AssignmentDate = dateUtc,
                VisitOrder = i + 1,
                Notes = request.Notes,
            };
            await _uow.SchoolAssignments.AddAsync(a);
            assignments.Add(a);
        }

        await _uow.SaveChangesAsync();

        // Fetch target user once — used for both lead creation and notifications
        var targetUser = await _uow.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == request.UserId);

        // Sync the lead per school. We want exactly ONE lead per (school, city) — the
        // assignee owns it. If a previous owner had a lead for this school, we transfer
        // ownership to the new assignee (preserves activities/deals history via FK).
        // Any extra duplicate leads from older bulk-assigns are deleted so the schools
        // list doesn't show the same school multiple times.
        if (targetUser != null)
        {
            try
            {
                var schools = await _uow.Schools.Query()
                    .Where(s => request.SchoolIds.Contains(s.Id))
                    .ToListAsync();

                foreach (var school in schools)
                {
                    var existingLeads = await _uow.Leads.Query()
                        .Where(l => l.School == school.Name && l.City == school.City)
                        .OrderByDescending(l => l.UpdatedAt)
                        .ToListAsync();

                    if (existingLeads.Count == 0)
                    {
                        var lead = new Lead
                        {
                            School = school.Name,
                            Board = school.Board ?? "",
                            City = school.City ?? "",
                            State = school.State ?? "",
                            Students = school.StudentCount ?? 0,
                            Type = school.Type ?? "Private",
                            Stage = Core.Enums.LeadStage.NewLead,
                            Score = 10,
                            Source = "SchoolAssignment",
                            FoId = request.UserId,
                            AssignedById = assignedById,
                            ContactName = school.PrincipalName ?? "",
                            ContactPhone = school.Phone ?? "",
                            ContactEmail = school.Email ?? "",
                            ContactDesignation = "Principal",
                        };
                        await _uow.Leads.AddAsync(lead);
                    }
                    else
                    {
                        // Keep the most-recently-updated lead, transfer it to the new owner.
                        var keep = existingLeads[0];
                        if (keep.FoId != request.UserId)
                        {
                            keep.FoId = request.UserId;
                            keep.AssignedById = assignedById;
                        }
                        // Drop any extra duplicates. DB cascade removes their Activities/Deals.
                        for (int i = 1; i < existingLeads.Count; i++)
                            await _uow.Leads.DeleteAsync(existingLeads[i]);
                    }
                }
                await _uow.SaveChangesAsync();
            }
            catch { /* best-effort lead sync */ }
        }

        // If someone assigns schools to another user, notify target + their direct manager
        if (assignedById != request.UserId)
        {
            try
            {
                var assigner = await _uow.Users.GetByIdAsync(assignedById);
                var schoolNames = await _uow.Schools.Query()
                    .Where(s => request.SchoolIds.Contains(s.Id))
                    .Select(s => s.Name).ToListAsync();

                // Always notify the target user
                await _notificationService.CreateNotificationAsync(
                    request.UserId,
                    Core.Enums.NotificationType.Info,
                    "Schools Assigned to You",
                    $"{assigner?.Name ?? "Someone"} assigned {schoolNames.Count} school(s) to you for {request.AssignmentDate}: {string.Join(", ", schoolNames)}"
                );

                // Notify the target's direct manager based on their role
                if (targetUser?.Role == Core.Enums.UserRole.FO && targetUser.ZoneId != null)
                {
                    // FO target → notify their ZH
                    var zh = await _uow.Users.Query()
                        .FirstOrDefaultAsync(u => u.Role == Core.Enums.UserRole.ZH && u.ZoneId == targetUser.ZoneId);
                    if (zh != null && zh.Id != assignedById)
                        await _notificationService.CreateNotificationAsync(zh.Id, Core.Enums.NotificationType.Info,
                            "School Assignment", $"{assigner?.Name ?? "Someone"} assigned {schoolNames.Count} school(s) to {targetUser.Name} for {request.AssignmentDate}");
                }
                else if (targetUser?.Role == Core.Enums.UserRole.ZH && targetUser.RegionId != null)
                {
                    // ZH target → notify their RH
                    var rh = await _uow.Users.Query()
                        .FirstOrDefaultAsync(u => u.Role == Core.Enums.UserRole.RH && u.RegionId == targetUser.RegionId);
                    if (rh != null && rh.Id != assignedById)
                        await _notificationService.CreateNotificationAsync(rh.Id, Core.Enums.NotificationType.Info,
                            "School Assignment", $"{assigner?.Name ?? "Someone"} assigned {schoolNames.Count} school(s) to ZH {targetUser.Name} for {request.AssignmentDate}");
                }
            }
            catch { }
        }

        // If FO self-assigned, notify their ZH
        if (assignedById == request.UserId && targetUser?.Role == Core.Enums.UserRole.FO)
        {
            try
            {
                if (targetUser.ZoneId != null)
                {
                    var zh = await _uow.Users.Query()
                        .FirstOrDefaultAsync(u => u.Role == Core.Enums.UserRole.ZH && u.ZoneId == targetUser.ZoneId);
                    if (zh != null)
                    {
                        var schoolNames = await _uow.Schools.Query()
                            .Where(s => request.SchoolIds.Contains(s.Id))
                            .Select(s => s.Name)
                            .ToListAsync();
                        await _notificationService.CreateNotificationAsync(
                            zh.Id,
                            Core.Enums.NotificationType.Info,
                            "FO Self-Assigned Schools",
                            $"{targetUser.Name} assigned {schoolNames.Count} school(s) to themselves for {request.AssignmentDate}: {string.Join(", ", schoolNames)}"
                        );
                    }
                }
            }
            catch { /* best-effort notification */ }
        }

        return await GetAssignmentsAsync(request.UserId, request.AssignmentDate);
    }

    // Reassign one school to a new user on a given date. Unlike BulkAssign, this is *additive*
    // for the new user — it appends to whatever they already had planned. The previous assignee
    // for this school+date (if any) is removed.
    public async Task<SchoolAssignmentDto> ReassignSingleAsync(int assignedById, ReassignSingleRequest request)
    {
        if (!DateTime.TryParse(request.AssignmentDate, out var date))
            throw new ArgumentException("Invalid date format");

        var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // 1. Remove any existing assignment of this school on this date (across all users).
        var prior = await _uow.SchoolAssignments.Query()
            .Where(a => a.SchoolId == request.SchoolId && a.AssignmentDate.Date == dateUtc.Date)
            .ToListAsync();
        foreach (var p in prior)
            await _uow.SchoolAssignments.DeleteAsync(p);

        // 2. Append a new assignment for the target user — preserve their other assignments.
        var nextOrder = await _uow.SchoolAssignments.Query()
            .Where(a => a.UserId == request.NewUserId && a.AssignmentDate.Date == dateUtc.Date)
            .CountAsync();

        var assignment = new SchoolAssignment
        {
            SchoolId = request.SchoolId,
            UserId = request.NewUserId,
            AssignedById = assignedById,
            AssignmentDate = dateUtc,
            VisitOrder = nextOrder + 1,
            Notes = request.Notes,
        };
        await _uow.SchoolAssignments.AddAsync(assignment);
        await _uow.SaveChangesAsync();

        // 3. Sync the Lead for this school — keep exactly one. If a previous owner had
        //    a lead, transfer it (preserves Activities/Deals); drop any duplicates.
        //    Only create a new lead when no lead exists for this school yet.
        var targetUser = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == request.NewUserId);
        var school = await _uow.Schools.Query().FirstOrDefaultAsync(s => s.Id == request.SchoolId);
        if (targetUser != null && school != null)
        {
            try
            {
                var existingLeads = await _uow.Leads.Query()
                    .Where(l => l.School == school.Name && l.City == school.City)
                    .OrderByDescending(l => l.UpdatedAt)
                    .ToListAsync();

                if (existingLeads.Count == 0)
                {
                    var lead = new Lead
                    {
                        School = school.Name,
                        Board = school.Board ?? "",
                        City = school.City ?? "",
                        State = school.State ?? "",
                        Students = school.StudentCount ?? 0,
                        Type = school.Type ?? "Private",
                        Stage = Core.Enums.LeadStage.NewLead,
                        Score = 10,
                        Source = "SchoolAssignment",
                        FoId = request.NewUserId,
                        AssignedById = assignedById,
                        ContactName = school.PrincipalName ?? "",
                        ContactPhone = school.Phone ?? "",
                        ContactEmail = school.Email ?? "",
                        ContactDesignation = "Principal",
                    };
                    await _uow.Leads.AddAsync(lead);
                    await _uow.SaveChangesAsync();
                }
                else
                {
                    var keep = existingLeads[0];
                    if (keep.FoId != request.NewUserId)
                    {
                        keep.FoId = request.NewUserId;
                        keep.AssignedById = assignedById;
                    }
                    for (int i = 1; i < existingLeads.Count; i++)
                        await _uow.Leads.DeleteAsync(existingLeads[i]);
                    await _uow.SaveChangesAsync();
                }
            }
            catch { /* best-effort lead creation */ }
        }

        // Notify target user
        if (assignedById != request.NewUserId && targetUser != null && school != null && _notificationService != null)
        {
            try
            {
                var assigner = await _uow.Users.GetByIdAsync(assignedById);
                await _notificationService.CreateNotificationAsync(
                    request.NewUserId, Core.Enums.NotificationType.Info,
                    "School Reassigned to You",
                    $"{assigner?.Name ?? "Someone"} reassigned {school.Name} to you for {request.AssignmentDate}.");
            }
            catch { }
        }

        // Re-fetch with includes for the DTO
        var saved = await _uow.SchoolAssignments.Query()
            .Include(a => a.School).Include(a => a.User).Include(a => a.AssignedBy)
            .FirstAsync(a => a.Id == assignment.Id);
        return MapToDto(saved);
    }

    public async Task<List<SchoolAssignmentDto>> GetAssignmentsAsync(int userId, string date)
    {
        if (!DateTime.TryParse(date, out var d)) return new();
        var dateUtc = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);

        var assignments = await _uow.SchoolAssignments.Query()
            .Include(a => a.School)
            .Include(a => a.User)
            .Include(a => a.AssignedBy)
            .Where(a => a.UserId == userId && a.AssignmentDate.Date == dateUtc.Date)
            .OrderBy(a => a.VisitOrder)
            .ToListAsync();

        return assignments.Select(MapToDto).ToList();
    }

    public async Task<List<SchoolAssignmentDto>> GetAssignmentsByManagerAsync(int managerId, string managerRole, string date)
    {
        if (!DateTime.TryParse(date, out var d)) return new();
        var dateUtc = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);

        var manager = await _uow.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == managerId);
        if (manager == null) return new();

        var query = _uow.SchoolAssignments.Query()
            .Include(a => a.School)
            .Include(a => a.User)
            .Include(a => a.AssignedBy)
            .Where(a => a.AssignmentDate.Date == dateUtc.Date);

        // Scope by role
        if (managerRole == "ZH" && manager.ZoneId.HasValue)
            query = query.Where(a => a.User.ZoneId == manager.ZoneId);
        else if (managerRole == "RH" && manager.RegionId.HasValue)
            query = query.Where(a => a.User.RegionId == manager.RegionId);
        else if (managerRole != "SH" && managerRole != "SCA")
            query = query.Where(a => a.AssignedById == managerId);

        var assignments = await query.OrderBy(a => a.UserId).ThenBy(a => a.VisitOrder).ToListAsync();
        return assignments.Select(MapToDto).ToList();
    }

    public async Task<bool> DeleteAssignmentAsync(int assignmentId, int requesterId)
    {
        var assignment = await _uow.SchoolAssignments.Query()
            .FirstOrDefaultAsync(a => a.Id == assignmentId);
        if (assignment == null) return false;

        await _uow.SchoolAssignments.DeleteAsync(assignment);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task MarkVisitedAsync(int userId, int schoolId, DateTime visitedAt)
    {
        var todayIst = GetTodayIst();
        var todayUtc = DateTime.SpecifyKind(todayIst.Date, DateTimeKind.Utc);

        var assignment = await _uow.SchoolAssignments.Query()
            .Where(a => a.UserId == userId && a.SchoolId == schoolId
                        && a.AssignmentDate.Date == todayUtc.Date && !a.IsVisited)
            .FirstOrDefaultAsync();

        if (assignment != null)
        {
            assignment.IsVisited = true;
            assignment.VisitedAt = visitedAt;
            assignment.UpdatedAt = DateTime.UtcNow;
            await _uow.SchoolAssignments.UpdateAsync(assignment);
            await _uow.SaveChangesAsync();
        }
    }

    public async Task UpdateTimeSpentAsync(int userId, int schoolId, decimal durationMinutes)
    {
        var todayIst = GetTodayIst();
        var todayUtc = DateTime.SpecifyKind(todayIst.Date, DateTimeKind.Utc);

        var assignment = await _uow.SchoolAssignments.Query()
            .Where(a => a.UserId == userId && a.SchoolId == schoolId
                        && a.AssignmentDate.Date == todayUtc.Date)
            .FirstOrDefaultAsync();

        if (assignment != null)
        {
            assignment.TimeSpentMinutes = durationMinutes;
            assignment.UpdatedAt = DateTime.UtcNow;
            await _uow.SchoolAssignments.UpdateAsync(assignment);
            await _uow.SaveChangesAsync();
        }
    }

    private static SchoolAssignmentDto MapToDto(SchoolAssignment a) => new()
    {
        Id = a.Id,
        SchoolId = a.SchoolId,
        SchoolName = a.School?.Name ?? "",
        SchoolAddress = a.School?.Address,
        SchoolCity = a.School?.City,
        SchoolLatitude = a.School?.Latitude ?? 0,
        SchoolLongitude = a.School?.Longitude ?? 0,
        GeofenceRadiusMetres = a.School?.GeofenceRadiusMetres ?? 100,
        UserId = a.UserId,
        UserName = a.User?.Name ?? "",
        AssignedById = a.AssignedById,
        AssignedByName = a.AssignedBy?.Name ?? "",
        AssignmentDate = a.AssignmentDate.ToString("yyyy-MM-dd"),
        VisitOrder = a.VisitOrder,
        IsVisited = a.IsVisited,
        VisitedAt = a.VisitedAt,
        TimeSpentMinutes = a.TimeSpentMinutes,
        Notes = a.Notes,
    };
}
