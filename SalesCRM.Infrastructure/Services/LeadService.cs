using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class LeadService : ILeadService
{
    private readonly IUnitOfWork _unitOfWork;

    public LeadService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedResult<LeadListDto>> GetLeadsAsync(
        int userId, PaginationParams pagination, string? search, string? stage, string? source)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new PaginatedResult<LeadListDto>();

        var query = _unitOfWork.Leads.Query().Include(l => l.Fo).AsQueryable();

        // Role-based filtering
        query = user.Role switch
        {
            UserRole.FO => query.Where(l => l.FoId == userId),
            UserRole.ZH => query.Where(l => l.Fo.ZoneId == user.ZoneId),
            UserRole.RH => query.Where(l => l.Fo.RegionId == user.RegionId),
            UserRole.SH => query,
            _ => query
        };

        if (!string.IsNullOrEmpty(search))
            query = query.Where(l => l.School.Contains(search) || l.City.Contains(search) || l.ContactName.Contains(search));

        if (!string.IsNullOrEmpty(stage) && Enum.TryParse<LeadStage>(stage, true, out var stageEnum))
            query = query.Where(l => l.Stage == stageEnum);

        if (!string.IsNullOrEmpty(source))
            query = query.Where(l => l.Source == source);

        var totalCount = await query.CountAsync();

        var sortedQuery = pagination.SortBy?.ToLower() switch
        {
            "school" => pagination.SortDescending ? query.OrderByDescending(l => l.School) : query.OrderBy(l => l.School),
            "score" => pagination.SortDescending ? query.OrderByDescending(l => l.Score) : query.OrderBy(l => l.Score),
            "value" => pagination.SortDescending ? query.OrderByDescending(l => l.Value) : query.OrderBy(l => l.Value),
            "lastactivity" => pagination.SortDescending ? query.OrderByDescending(l => l.LastActivityDate) : query.OrderBy(l => l.LastActivityDate),
            _ => query.OrderByDescending(l => l.UpdatedAt)
        };

        var items = await sortedQuery
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(l => new LeadListDto
            {
                Id = l.Id,
                School = l.School,
                Board = l.Board,
                City = l.City,
                Type = l.Type,
                Stage = l.Stage.ToString(),
                Score = l.Score,
                Value = l.Value,
                LastActivityDate = l.LastActivityDate,
                Source = l.Source,
                FoId = l.FoId,
                FoName = l.Fo.Name,
                ContactName = l.ContactName
            })
            .ToListAsync();

        return new PaginatedResult<LeadListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
    }

    public async Task<LeadDto?> GetLeadByIdAsync(int id, int userId)
    {
        var lead = await _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Include(l => l.Activities.OrderByDescending(a => a.Date))
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lead == null) return null;

        return MapToLeadDto(lead);
    }

    public async Task<LeadDto> CreateLeadAsync(CreateLeadRequest request, int foId)
    {
        var fo = await _unitOfWork.Users.GetByIdAsync(foId);

        var lead = new Lead
        {
            School = request.School,
            Board = request.Board,
            City = request.City,
            State = request.State,
            Students = request.Students,
            Type = request.Type,
            Source = request.Source,
            Value = request.Value,
            CloseDate = request.CloseDate,
            Notes = request.Notes,
            Stage = LeadStage.NewLead,
            Score = CalculateInitialScore(request),
            FoId = foId,
            ContactName = request.ContactName,
            ContactDesignation = request.ContactDesignation,
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail
        };

        await _unitOfWork.Leads.AddAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        lead.Fo = fo!;
        return MapToLeadDto(lead);
    }

    public async Task<LeadDto?> UpdateLeadAsync(int id, UpdateLeadRequest request, int userId)
    {
        var lead = await _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lead == null) return null;

        if (request.School != null) lead.School = request.School;
        if (request.Board != null) lead.Board = request.Board;
        if (request.City != null) lead.City = request.City;
        if (request.State != null) lead.State = request.State;
        if (request.Students.HasValue) lead.Students = request.Students.Value;
        if (request.Type != null) lead.Type = request.Type;
        if (request.Value.HasValue) lead.Value = request.Value.Value;
        if (request.CloseDate.HasValue) lead.CloseDate = request.CloseDate.Value;
        if (request.Notes != null) lead.Notes = request.Notes;
        if (request.LossReason != null) lead.LossReason = request.LossReason;
        if (request.ContactName != null) lead.ContactName = request.ContactName;
        if (request.ContactDesignation != null) lead.ContactDesignation = request.ContactDesignation;
        if (request.ContactPhone != null) lead.ContactPhone = request.ContactPhone;
        if (request.ContactEmail != null) lead.ContactEmail = request.ContactEmail;

        if (request.Stage != null && Enum.TryParse<LeadStage>(request.Stage, true, out var stage))
            lead.Stage = stage;

        await _unitOfWork.Leads.UpdateAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        return MapToLeadDto(lead);
    }

    public async Task<bool> DeleteLeadAsync(int id, int userId)
    {
        var lead = await _unitOfWork.Leads.GetByIdAsync(id);
        if (lead == null) return false;

        await _unitOfWork.Leads.DeleteAsync(lead);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CheckDuplicateAsync(string school, string city)
    {
        return await _unitOfWork.Leads.Query()
            .AnyAsync(l => l.School.ToLower() == school.ToLower() && l.City.ToLower() == city.ToLower());
    }

    public async Task<List<LeadListDto>> GetLeadsByStageAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null) return new();

        var query = _unitOfWork.Leads.Query().Include(l => l.Fo).AsQueryable();
        query = user.Role == UserRole.FO ? query.Where(l => l.FoId == userId) : query;

        return await query
            .Where(l => l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost)
            .Select(l => new LeadListDto
            {
                Id = l.Id,
                School = l.School,
                Board = l.Board,
                City = l.City,
                Type = l.Type,
                Stage = l.Stage.ToString(),
                Score = l.Score,
                Value = l.Value,
                LastActivityDate = l.LastActivityDate,
                Source = l.Source,
                FoId = l.FoId,
                FoName = l.Fo.Name,
                ContactName = l.ContactName
            })
            .ToListAsync();
    }

    private static int CalculateInitialScore(CreateLeadRequest request)
    {
        int score = 20; // base
        if (request.Students > 1000) score += 15;
        else if (request.Students > 500) score += 10;
        if (request.Source == "Referral") score += 15;
        else if (request.Source == "Field Visit") score += 10;
        if (request.Value > 500000) score += 10;
        return Math.Min(score, 100);
    }

    private static LeadDto MapToLeadDto(Lead lead) => new()
    {
        Id = lead.Id,
        School = lead.School,
        Board = lead.Board,
        City = lead.City,
        State = lead.State,
        Students = lead.Students,
        Type = lead.Type,
        Stage = lead.Stage.ToString(),
        Score = lead.Score,
        Value = lead.Value,
        CloseDate = lead.CloseDate,
        LastActivityDate = lead.LastActivityDate,
        Source = lead.Source,
        Notes = lead.Notes,
        LossReason = lead.LossReason,
        FoId = lead.FoId,
        FoName = lead.Fo?.Name ?? string.Empty,
        Contact = new ContactDto
        {
            Name = lead.ContactName,
            Designation = lead.ContactDesignation,
            Phone = lead.ContactPhone,
            Email = lead.ContactEmail
        },
        Activities = lead.Activities?.Select(a => new ActivityDto
        {
            Id = a.Id,
            Type = a.Type.ToString(),
            Date = a.Date,
            Outcome = a.Outcome.ToString(),
            Notes = a.Notes,
            GpsVerified = a.GpsVerified,
            FoId = a.FoId,
            LeadId = a.LeadId
        }).ToList() ?? new()
    };
}
