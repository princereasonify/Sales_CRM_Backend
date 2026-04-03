using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Onboarding;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class OnboardService : IOnboardService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notify;
    public OnboardService(IUnitOfWork uow, INotificationService notify) { _uow = uow; _notify = notify; }

    private static OnboardAssignmentDto ToDto(OnboardAssignment o) => new()
    {
        Id = o.Id, LeadId = o.LeadId, LeadName = o.Lead?.School,
        DealId = o.DealId, SchoolId = o.SchoolId, SchoolName = o.School?.Name ?? "",
        AssignedToId = o.AssignedToId, AssignedToName = o.AssignedTo?.Name ?? "",
        AssignedById = o.AssignedById, AssignedByName = o.AssignedBy?.Name ?? "",
        ScheduledStartDate = o.ScheduledStartDate, ScheduledEndDate = o.ScheduledEndDate,
        Status = o.Status.ToString(), Modules = o.Modules,
        CompletionPercentage = o.CompletionPercentage, Notes = o.Notes, CreatedAt = o.CreatedAt
    };

    public async Task<(List<OnboardAssignmentDto> Items, int Total)> GetOnboardingsAsync(
        string? status, int? assignedToId, int page, int limit)
    {
        var q = _uow.OnboardAssignments.Query()
            .Include(o => o.Lead).Include(o => o.School)
            .Include(o => o.AssignedTo).Include(o => o.AssignedBy).AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OnboardStatus>(status, true, out var s))
            q = q.Where(o => o.Status == s);
        if (assignedToId.HasValue)
            q = q.Where(o => o.AssignedToId == assignedToId.Value);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * limit).Take(limit).ToListAsync();
        return (items.Select(ToDto).ToList(), total);
    }

    public async Task<OnboardAssignmentDto?> GetOnboardingByIdAsync(int id)
    {
        var o = await _uow.OnboardAssignments.Query()
            .Include(x => x.Lead).Include(x => x.School)
            .Include(x => x.AssignedTo).Include(x => x.AssignedBy)
            .FirstOrDefaultAsync(x => x.Id == id);
        return o == null ? null : ToDto(o);
    }

    public async Task<OnboardAssignmentDto> CreateOnboardingAsync(CreateOnboardRequest request, int assignedById)
    {
        var ob = new OnboardAssignment
        {
            LeadId = request.LeadId, DealId = request.DealId, SchoolId = request.SchoolId,
            AssignedToId = request.AssignedToId, AssignedById = assignedById,
            ScheduledStartDate = request.ScheduledStartDate.HasValue ? DateTime.SpecifyKind(request.ScheduledStartDate.Value, DateTimeKind.Utc) : null,
            ScheduledEndDate = request.ScheduledEndDate.HasValue ? DateTime.SpecifyKind(request.ScheduledEndDate.Value, DateTimeKind.Utc) : null,
            Modules = request.Modules, Notes = request.Notes
        };
        await _uow.OnboardAssignments.AddAsync(ob);
        await _uow.SaveChangesAsync();

        // Notify FO that onboarding is assigned
        try
        {
            var school = await _uow.Schools.GetByIdAsync(request.SchoolId);
            await _notify.CreateNotificationAsync(request.AssignedToId, NotificationType.Info,
                $"Onboarding assigned: {school?.Name ?? "School"}", $"You have been assigned onboarding for {school?.Name ?? "School"}.");
        }
        catch { }

        return (await GetOnboardingByIdAsync(ob.Id))!;
    }

    public async Task<OnboardAssignmentDto?> UpdateOnboardingAsync(int id, UpdateOnboardRequest request)
    {
        var o = await _uow.OnboardAssignments.GetByIdAsync(id);
        if (o == null) return null;

        if (request.Status != null && Enum.TryParse<OnboardStatus>(request.Status, true, out var st)) o.Status = st;
        if (request.CompletionPercentage.HasValue) o.CompletionPercentage = request.CompletionPercentage.Value;
        if (request.Notes != null) o.Notes = request.Notes;
        if (request.ScheduledStartDate.HasValue) o.ScheduledStartDate = DateTime.SpecifyKind(request.ScheduledStartDate.Value, DateTimeKind.Utc);
        if (request.ScheduledEndDate.HasValue) o.ScheduledEndDate = DateTime.SpecifyKind(request.ScheduledEndDate.Value, DateTimeKind.Utc);

        await _uow.SaveChangesAsync();

        // Notify ZH when onboarding is completed
        if (o.Status == OnboardStatus.Completed)
        {
            try
            {
                var fo = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == o.AssignedToId);
                var school = await _uow.Schools.GetByIdAsync(o.SchoolId);
                if (fo?.ZoneId != null)
                {
                    var zh = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == fo.ZoneId);
                    if (zh != null)
                        await _notify.CreateNotificationAsync(zh.Id, NotificationType.Success,
                            $"Onboarding completed: {school?.Name ?? "School"}", $"{fo.Name} completed onboarding for {school?.Name ?? "School"}.");
                }
            }
            catch { }
        }

        return await GetOnboardingByIdAsync(id);
    }
}
