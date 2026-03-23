using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Demos;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class DemoService : IDemoService
{
    private readonly IUnitOfWork _uow;
    public DemoService(IUnitOfWork uow) => _uow = uow;

    private static DemoAssignmentDto ToDto(DemoAssignment d) => new()
    {
        Id = d.Id, LeadId = d.LeadId, LeadName = d.Lead?.School,
        SchoolId = d.SchoolId, SchoolName = d.School?.Name ?? "",
        RequestedById = d.RequestedById, RequestedByName = d.RequestedBy?.Name ?? "",
        AssignedToId = d.AssignedToId, AssignedToName = d.AssignedTo?.Name ?? "",
        ApprovedById = d.ApprovedById, ApprovedByName = d.ApprovedBy?.Name,
        ScheduledDate = d.ScheduledDate,
        ScheduledStartTime = d.ScheduledStartTime.ToString(@"hh\:mm"),
        ScheduledEndTime = d.ScheduledEndTime.ToString(@"hh\:mm"),
        DemoMode = d.DemoMode, Status = d.Status.ToString(),
        MeetingLink = d.MeetingLink, Notes = d.Notes, Feedback = d.Feedback,
        Outcome = d.Outcome?.ToString(), CompletedAt = d.CompletedAt, CreatedAt = d.CreatedAt
    };

    public async Task<(List<DemoAssignmentDto> Demos, int Total)> GetDemosAsync(
        string? status, int? assignedToId, string? from, string? to, int page, int limit)
    {
        var q = _uow.DemoAssignments.Query()
            .Include(d => d.Lead).Include(d => d.School)
            .Include(d => d.RequestedBy).Include(d => d.AssignedTo).Include(d => d.ApprovedBy)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DemoStatus>(status, true, out var s))
            q = q.Where(d => d.Status == s);
        if (assignedToId.HasValue)
            q = q.Where(d => d.AssignedToId == assignedToId.Value);
        if (DateTime.TryParse(from, out var fd))
            q = q.Where(d => d.ScheduledDate >= DateTime.SpecifyKind(fd.Date, DateTimeKind.Utc));
        if (DateTime.TryParse(to, out var td))
            q = q.Where(d => d.ScheduledDate <= DateTime.SpecifyKind(td.Date, DateTimeKind.Utc));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(d => d.ScheduledDate).Skip((page - 1) * limit).Take(limit).ToListAsync();
        return (items.Select(ToDto).ToList(), total);
    }

    public async Task<DemoAssignmentDto?> GetDemoByIdAsync(int id)
    {
        var d = await _uow.DemoAssignments.Query()
            .Include(x => x.Lead).Include(x => x.School)
            .Include(x => x.RequestedBy).Include(x => x.AssignedTo).Include(x => x.ApprovedBy)
            .FirstOrDefaultAsync(x => x.Id == id);
        return d == null ? null : ToDto(d);
    }

    public async Task<DemoAssignmentDto> CreateDemoAsync(CreateDemoRequest request, int requestedById)
    {
        var demo = new DemoAssignment
        {
            LeadId = request.LeadId, SchoolId = request.SchoolId,
            RequestedById = requestedById, AssignedToId = request.AssignedToId,
            ScheduledDate = DateTime.SpecifyKind(request.ScheduledDate.Date, DateTimeKind.Utc),
            ScheduledStartTime = TimeSpan.Parse(request.ScheduledStartTime),
            ScheduledEndTime = TimeSpan.Parse(request.ScheduledEndTime),
            DemoMode = request.DemoMode, MeetingLink = request.MeetingLink, Notes = request.Notes,
            Status = DemoStatus.Requested
        };
        await _uow.DemoAssignments.AddAsync(demo);
        await _uow.SaveChangesAsync();
        return (await GetDemoByIdAsync(demo.Id))!;
    }

    public async Task<DemoAssignmentDto?> UpdateDemoAsync(int id, UpdateDemoRequest request)
    {
        var d = await _uow.DemoAssignments.GetByIdAsync(id);
        if (d == null) return null;

        if (request.Status != null && Enum.TryParse<DemoStatus>(request.Status, true, out var st)) d.Status = st;
        if (request.Feedback != null) d.Feedback = request.Feedback;
        if (request.Outcome != null && Enum.TryParse<DemoOutcome>(request.Outcome, true, out var oc)) d.Outcome = oc;
        if (request.Notes != null) d.Notes = request.Notes;
        if (request.ScheduledDate.HasValue) d.ScheduledDate = DateTime.SpecifyKind(request.ScheduledDate.Value.Date, DateTimeKind.Utc);
        if (request.ScheduledStartTime != null) d.ScheduledStartTime = TimeSpan.Parse(request.ScheduledStartTime);
        if (request.ScheduledEndTime != null) d.ScheduledEndTime = TimeSpan.Parse(request.ScheduledEndTime);
        if (request.MeetingLink != null) d.MeetingLink = request.MeetingLink;
        if (request.AssignedToId.HasValue) d.AssignedToId = request.AssignedToId.Value;
        if (d.Status == DemoStatus.Completed && d.CompletedAt == null) d.CompletedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();
        return await GetDemoByIdAsync(id);
    }

    public async Task<List<DemoAssignmentDto>> GetDemoCalendarAsync(string from, string to, int userId)
    {
        if (!DateTime.TryParse(from, out var fd) || !DateTime.TryParse(to, out var td)) return new();
        var items = await _uow.DemoAssignments.Query()
            .Include(d => d.Lead).Include(d => d.School).Include(d => d.RequestedBy).Include(d => d.AssignedTo)
            .Where(d => d.ScheduledDate >= DateTime.SpecifyKind(fd.Date, DateTimeKind.Utc) &&
                        d.ScheduledDate <= DateTime.SpecifyKind(td.Date, DateTimeKind.Utc) &&
                        (d.AssignedToId == userId || d.RequestedById == userId))
            .OrderBy(d => d.ScheduledDate).ThenBy(d => d.ScheduledStartTime)
            .ToListAsync();
        return items.Select(ToDto).ToList();
    }
}
