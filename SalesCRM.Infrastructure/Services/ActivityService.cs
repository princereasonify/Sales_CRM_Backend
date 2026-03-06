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

    public ActivityService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
            FoId = foId,
            LeadId = request.LeadId
        };

        await _unitOfWork.Activities.AddAsync(activity);

        // Update lead's last activity date
        lead.LastActivityDate = request.Date;
        await _unitOfWork.Leads.UpdateAsync(lead);

        await _unitOfWork.SaveChangesAsync();

        var fo = await _unitOfWork.Users.GetByIdAsync(foId);

        return new ActivityDto
        {
            Id = activity.Id,
            Type = activity.Type.ToString(),
            Date = activity.Date,
            Outcome = activity.Outcome.ToString(),
            Notes = activity.Notes,
            GpsVerified = activity.GpsVerified,
            FoId = foId,
            FoName = fo?.Name ?? string.Empty,
            LeadId = activity.LeadId,
            School = lead.School
        };
    }
}
