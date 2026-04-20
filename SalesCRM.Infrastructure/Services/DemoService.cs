using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Demos;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class DemoService : IDemoService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notify;
    public DemoService(IUnitOfWork uow, INotificationService notify) { _uow = uow; _notify = notify; }

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
        FeedbackSentiment = d.FeedbackSentiment, FeedbackAudioUrl = d.FeedbackAudioUrl,
        FeedbackVideoUrl = d.FeedbackVideoUrl, ScreenRecordingUrl = d.ScreenRecordingUrl,
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
            LeadId = request.LeadId > 0 ? request.LeadId : null, SchoolId = request.SchoolId,
            RequestedById = requestedById, AssignedToId = request.AssignedToId,
            ScheduledDate = DateTime.SpecifyKind(request.ScheduledDate.Date, DateTimeKind.Utc),
            ScheduledStartTime = TimeSpan.Parse(request.ScheduledStartTime),
            ScheduledEndTime = TimeSpan.Parse(request.ScheduledEndTime),
            DemoMode = request.DemoMode, MeetingLink = request.MeetingLink, Notes = request.Notes,
            Status = DemoStatus.Requested
        };
        await _uow.DemoAssignments.AddAsync(demo);
        await _uow.SaveChangesAsync();

        // Notify FO that demo is assigned to them
        if (request.AssignedToId != requestedById)
        {
            try
            {
                var school = await _uow.Schools.GetByIdAsync(request.SchoolId);
                var requester = await _uow.Users.GetByIdAsync(requestedById);
                await _notify.CreateNotificationAsync(request.AssignedToId, NotificationType.Info,
                    $"Demo assigned: {school?.Name ?? "School"}",
                    $"{requester?.Name ?? "Manager"} assigned a demo to you at {school?.Name ?? "School"} on {demo.ScheduledDate:dd MMM yyyy}.");
            }
            catch { }
        }

        return (await GetDemoByIdAsync(demo.Id))!;
    }

    public async Task<DemoAssignmentDto?> UpdateDemoAsync(int id, UpdateDemoRequest request, int userId)
    {
        var d = await _uow.DemoAssignments.GetByIdAsync(id);
        if (d == null) return null;

        // Authorization: only the assigned FO, the requester (manager who set it up), or managers in scope
        var caller = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new UnauthorizedAccessException("User not found");

        var assignedFo = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == d.AssignedToId);

        var authorized = caller.Role switch
        {
            UserRole.FO => d.AssignedToId == userId,
            UserRole.ZH => assignedFo?.ZoneId == caller.ZoneId || d.RequestedById == userId,
            UserRole.RH => assignedFo?.RegionId == caller.RegionId || d.RequestedById == userId,
            UserRole.SH or UserRole.SCA => true,
            _ => false
        };
        if (!authorized)
            throw new UnauthorizedAccessException("You don't have permission to update this demo");

        if (request.Status != null && Enum.TryParse<DemoStatus>(request.Status, true, out var st))
        {
            // Enforce valid state transitions
            var allowed = d.Status switch
            {
                DemoStatus.Requested => new[] { DemoStatus.Approved, DemoStatus.Scheduled, DemoStatus.Cancelled },
                DemoStatus.Approved => new[] { DemoStatus.Scheduled, DemoStatus.Cancelled },
                DemoStatus.Scheduled => new[] { DemoStatus.InProgress, DemoStatus.Rescheduled, DemoStatus.Cancelled, DemoStatus.Completed },
                DemoStatus.Rescheduled => new[] { DemoStatus.Scheduled, DemoStatus.InProgress, DemoStatus.Cancelled },
                DemoStatus.InProgress => new[] { DemoStatus.Completed, DemoStatus.Cancelled },
                DemoStatus.Completed => Array.Empty<DemoStatus>(),
                DemoStatus.Cancelled => Array.Empty<DemoStatus>(),
                _ => Array.Empty<DemoStatus>()
            };

            if (st != d.Status && !allowed.Contains(st))
                throw new InvalidOperationException($"Cannot transition demo from {d.Status} to {st}");

            d.Status = st;
        }
        if (request.Feedback != null) d.Feedback = request.Feedback;
        if (request.FeedbackSentiment != null) d.FeedbackSentiment = request.FeedbackSentiment;
        if (request.FeedbackAudioUrl != null) d.FeedbackAudioUrl = request.FeedbackAudioUrl;
        if (request.FeedbackVideoUrl != null) d.FeedbackVideoUrl = request.FeedbackVideoUrl;
        if (request.ScreenRecordingUrl != null) d.ScreenRecordingUrl = request.ScreenRecordingUrl;
        if (request.Outcome != null && Enum.TryParse<DemoOutcome>(request.Outcome, true, out var oc)) d.Outcome = oc;
        if (request.Notes != null) d.Notes = request.Notes;
        if (request.ScheduledDate.HasValue) d.ScheduledDate = DateTime.SpecifyKind(request.ScheduledDate.Value.Date, DateTimeKind.Utc);
        if (request.ScheduledStartTime != null) d.ScheduledStartTime = TimeSpan.Parse(request.ScheduledStartTime);
        if (request.ScheduledEndTime != null) d.ScheduledEndTime = TimeSpan.Parse(request.ScheduledEndTime);
        if (request.MeetingLink != null) d.MeetingLink = request.MeetingLink;
        if (request.AssignedToId.HasValue) d.AssignedToId = request.AssignedToId.Value;
        if (d.Status == DemoStatus.Completed && d.CompletedAt == null)
        {
            d.CompletedAt = DateTime.UtcNow;
            // Notify ZH that demo was completed
            try
            {
                var fo = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == d.AssignedToId);
                if (fo?.ZoneId != null)
                {
                    var zh = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == fo.ZoneId);
                    var school = await _uow.Schools.GetByIdAsync(d.SchoolId);
                    if (zh != null)
                        await _notify.CreateNotificationAsync(zh.Id, NotificationType.Success,
                            $"Demo completed: {school?.Name ?? "School"}", $"{fo.Name} completed a demo at {school?.Name ?? "School"}.");
                }
            }
            catch { }
        }

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
