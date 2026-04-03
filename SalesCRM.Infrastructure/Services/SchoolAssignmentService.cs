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

        // Remove existing assignments for this FO on this date
        var existing = await _uow.SchoolAssignments.Query()
            .Where(a => a.UserId == request.UserId && a.AssignmentDate.Date == dateUtc.Date)
            .ToListAsync();

        foreach (var e in existing)
            await _uow.SchoolAssignments.DeleteAsync(e);

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

        // Auto-create leads for assigned schools if not already exists for this FO
        try
        {
            var schools = await _uow.Schools.Query()
                .Where(s => request.SchoolIds.Contains(s.Id))
                .ToListAsync();

            foreach (var school in schools)
            {
                var leadExists = await _uow.Leads.Query()
                    .AnyAsync(l => l.School == school.Name && l.City == school.City && l.FoId == request.UserId);

                if (!leadExists)
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
            }
            await _uow.SaveChangesAsync();
        }
        catch { /* best-effort lead creation */ }

        // If manager assigns schools to FO, notify the FO
        if (assignedById != request.UserId)
        {
            try
            {
                var manager = await _uow.Users.GetByIdAsync(assignedById);
                var schoolNames = await _uow.Schools.Query()
                    .Where(s => request.SchoolIds.Contains(s.Id))
                    .Select(s => s.Name).ToListAsync();
                await _notificationService.CreateNotificationAsync(
                    request.UserId,
                    Core.Enums.NotificationType.Info,
                    $"Schools Assigned to You",
                    $"{manager?.Name ?? "Manager"} assigned {schoolNames.Count} school(s) to you for {request.AssignmentDate}: {string.Join(", ", schoolNames)}"
                );
            }
            catch { }
        }

        // If FO self-assigned, notify their ZH
        if (assignedById == request.UserId)
        {
            try
            {
                var fo = await _uow.Users.Query()
                    .FirstOrDefaultAsync(u => u.Id == request.UserId);
                if (fo?.ZoneId != null)
                {
                    var zh = await _uow.Users.Query()
                        .FirstOrDefaultAsync(u => u.Role == Core.Enums.UserRole.ZH && u.ZoneId == fo.ZoneId);
                    if (zh != null)
                    {
                        var schoolNames = await _uow.Schools.Query()
                            .Where(s => request.SchoolIds.Contains(s.Id))
                            .Select(s => s.Name)
                            .ToListAsync();
                        await _notificationService.CreateNotificationAsync(
                            zh.Id,
                            Core.Enums.NotificationType.Info,
                            $"FO Self-Assigned Schools",
                            $"{fo.Name} assigned {schoolNames.Count} school(s) to themselves for {request.AssignmentDate}: {string.Join(", ", schoolNames)}"
                        );
                    }
                }
            }
            catch { /* best-effort notification */ }
        }

        return await GetAssignmentsAsync(request.UserId, request.AssignmentDate);
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
