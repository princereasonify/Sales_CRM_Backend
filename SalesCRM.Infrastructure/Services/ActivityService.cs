using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class ActivityService : IActivityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public ActivityService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<PaginatedResult<ActivityDto>> GetActivitiesAsync(
        int foId, PaginationParams pagination, string? type)
    {
        var query = _unitOfWork.Activities.Query()
            .Include(a => a.Fo)
            .Include(a => a.Lead)
            .Where(a => a.FoId == foId);

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<ActivityType>(type, true, out var actType))
            query = query.Where(a => a.Type == actType);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.Date)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                Type = a.Type.ToString(),
                Date = a.Date,
                Outcome = a.Outcome.ToString(),
                Notes = a.Notes,
                GpsVerified = a.GpsVerified,
                TimeIn = a.TimeIn,
                TimeOut = a.TimeOut,
                PersonMet = a.PersonMet,
                PersonDesignation = a.PersonDesignation,
                PersonPhone = a.PersonPhone,
                InterestLevel = a.InterestLevel,
                NextAction = a.NextAction,
                NextFollowUpDate = a.NextFollowUpDate,
                PhotoUrl = a.PhotoUrl,
                DemoMode = a.DemoMode,
                ConductedBy = a.ConductedBy,
                Attendees = a.Attendees,
                Feedback = a.Feedback,
                FoId = a.FoId,
                FoName = a.Fo.Name,
                LeadId = a.LeadId,
                School = a.Lead.School
            })
            .ToListAsync();

        return new PaginatedResult<ActivityDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
    }

    public async Task<List<ActivityDto>> GetTeamActivitiesAsync(int managerId, string managerRole, int foId)
    {
        // Verify the manager has scope over this FO
        var manager = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == managerId);
        var fo = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == foId && u.Role == UserRole.FO);

        if (manager == null || fo == null) return new();

        // Scope check based on role
        if (managerRole == "ZH" && fo.ZoneId != manager.ZoneId) return new();
        if (managerRole == "RH" && fo.RegionId != manager.RegionId) return new();
        // SH can see all

        // Return recent activities (last 30 days) for this FO
        var since = DateTime.UtcNow.AddDays(-30);
        return await _unitOfWork.Activities.Query()
            .Include(a => a.Fo)
            .Include(a => a.Lead)
            .Where(a => a.FoId == foId && a.Date >= since)
            .OrderByDescending(a => a.Date)
            .Take(50)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                Type = a.Type.ToString(),
                Date = a.Date,
                Outcome = a.Outcome.ToString(),
                Notes = a.Notes,
                GpsVerified = a.GpsVerified,
                TimeIn = a.TimeIn,
                TimeOut = a.TimeOut,
                PersonMet = a.PersonMet,
                PersonDesignation = a.PersonDesignation,
                PersonPhone = a.PersonPhone,
                InterestLevel = a.InterestLevel,
                NextAction = a.NextAction,
                NextFollowUpDate = a.NextFollowUpDate,
                PhotoUrl = a.PhotoUrl,
                DemoMode = a.DemoMode,
                ConductedBy = a.ConductedBy,
                Attendees = a.Attendees,
                Feedback = a.Feedback,
                FoId = a.FoId,
                FoName = a.Fo.Name,
                LeadId = a.LeadId,
                School = a.Lead.School
            })
            .ToListAsync();
    }

    public async Task<ActivityDto> CreateActivityAsync(CreateActivityRequest request, int foId)
    {
        if (!Enum.TryParse<ActivityType>(request.Type, true, out var actType))
            throw new ArgumentException($"Invalid activity type: {request.Type}");

        if (!Enum.TryParse<ActivityOutcome>(request.Outcome, true, out var outcome))
            throw new ArgumentException($"Invalid outcome: {request.Outcome}");

        var lead = await _unitOfWork.Leads.GetByIdAsync(request.LeadId)
            ?? throw new ArgumentException("Lead not found");

        var activity = new Activity
        {
            Type = actType,
            Date = request.Date,
            Outcome = outcome,
            Notes = request.Notes,
            GpsVerified = request.GpsVerified,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TimeIn = request.TimeIn,
            TimeOut = request.TimeOut,
            PersonMet = request.PersonMet,
            PersonDesignation = request.PersonDesignation,
            PersonPhone = request.PersonPhone,
            InterestLevel = request.InterestLevel,
            NextAction = request.NextAction,
            NextFollowUpDate = request.NextFollowUpDate,
            DemoMode = request.DemoMode,
            ConductedBy = request.ConductedBy,
            Attendees = request.Attendees,
            Feedback = request.Feedback,
            FoId = foId,
            LeadId = request.LeadId
        };

        await _unitOfWork.Activities.AddAsync(activity);

        // Update lead's last activity date
        lead.LastActivityDate = request.Date;
        await _unitOfWork.Leads.UpdateAsync(lead);

        await _unitOfWork.SaveChangesAsync();

        // Create follow-up reminder notification if follow-up date is set
        if (request.NextFollowUpDate.HasValue)
        {
            await _notificationService.CreateNotificationAsync(
                foId,
                NotificationType.Reminder,
                $"Follow-up scheduled: {lead.School}",
                $"You have a {request.NextAction ?? request.Type} follow-up on {request.NextFollowUpDate.Value:MMM dd, yyyy} for {lead.School}."
            );
        }

        // Notify ZH about the activity
        var foUser = await _unitOfWork.Users.GetByIdAsync(foId);
        if (foUser?.ZoneId != null)
        {
            var zh = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == foUser.ZoneId);
            if (zh != null)
            {
                await _notificationService.CreateNotificationAsync(
                    zh.Id,
                    NotificationType.Info,
                    $"Activity logged: {lead.School}",
                    $"{foUser.Name} logged a {actType} activity for {lead.School}."
                );
            }
        }

        var fo = foUser ?? await _unitOfWork.Users.GetByIdAsync(foId);

        return new ActivityDto
        {
            Id = activity.Id,
            Type = activity.Type.ToString(),
            Date = activity.Date,
            Outcome = activity.Outcome.ToString(),
            Notes = activity.Notes,
            GpsVerified = activity.GpsVerified,
            TimeIn = activity.TimeIn,
            TimeOut = activity.TimeOut,
            PersonMet = activity.PersonMet,
            PersonDesignation = activity.PersonDesignation,
            PersonPhone = activity.PersonPhone,
            InterestLevel = activity.InterestLevel,
            NextAction = activity.NextAction,
            NextFollowUpDate = activity.NextFollowUpDate,
            PhotoUrl = activity.PhotoUrl,
            DemoMode = activity.DemoMode,
            ConductedBy = activity.ConductedBy,
            Attendees = activity.Attendees,
            Feedback = activity.Feedback,
            FoId = foId,
            FoName = fo?.Name ?? string.Empty,
            LeadId = activity.LeadId,
            School = lead.School
        };
    }

    public async Task UpdatePhotoUrlAsync(int activityId, int userId, string photoUrl)
    {
        var activity = await _unitOfWork.Activities.Query()
            .FirstOrDefaultAsync(a => a.Id == activityId && a.FoId == userId);

        if (activity != null)
        {
            activity.PhotoUrl = photoUrl;
            await _unitOfWork.Activities.UpdateAsync(activity);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
