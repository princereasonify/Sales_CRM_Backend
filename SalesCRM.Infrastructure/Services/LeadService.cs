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
    private readonly INotificationService _notificationService;

    public LeadService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<PaginatedResult<LeadListDto>> GetLeadsAsync(
        int userId, PaginationParams pagination, string? search, string? stage, string? source)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new PaginatedResult<LeadListDto>();

        var query = _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Include(l => l.AssignedBy)
            .AsQueryable();

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
                AssignedById = l.AssignedById,
                AssignedByName = l.AssignedBy != null ? l.AssignedBy.Name : null,
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
            .Include(l => l.AssignedBy)
            .Include(l => l.Activities.OrderByDescending(a => a.Date))
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lead == null) return null;

        var school = await FindLinkedSchoolAsync(lead);
        return MapToLeadDto(lead, school);
    }

    public async Task<LeadDto> CreateLeadAsync(CreateLeadRequest request, int creatorId, string creatorRole)
    {
        int targetFoId;
        int? assignedById = null;

        if (creatorRole == "FO")
        {
            // FO creates lead for themselves
            targetFoId = creatorId;
        }
        else
        {
            // Manager (ZH/RH/SH) must specify which FO to assign the lead to
            if (!request.FoId.HasValue)
                throw new InvalidOperationException("FoId is required when a manager creates a lead");

            targetFoId = request.FoId.Value;
            assignedById = creatorId;

            // Validate the target FO exists and is actually an FO
            var targetFo = await _unitOfWork.Users.GetByIdAsync(targetFoId);
            if (targetFo == null || targetFo.Role != UserRole.FO)
                throw new InvalidOperationException("Target user is not a valid Field Officer");

            // Validate the manager can see this FO (same zone/region scope)
            var creator = await _unitOfWork.Users.Query()
                .Include(u => u.Zone)
                .FirstOrDefaultAsync(u => u.Id == creatorId);

            if (creator == null)
                throw new InvalidOperationException("Creator not found");

            if (creatorRole == "ZH" && targetFo.ZoneId != creator.ZoneId)
                throw new InvalidOperationException("You can only assign leads to FOs in your zone");

            if (creatorRole == "RH" && targetFo.RegionId != creator.RegionId)
                throw new InvalidOperationException("You can only assign leads to FOs in your region");

            // SH can assign to any FO — no scope restriction
        }

        var fo = await _unitOfWork.Users.GetByIdAsync(targetFoId);

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
            FoId = targetFoId,
            AssignedById = assignedById,
            ContactName = request.ContactName,
            ContactDesignation = request.ContactDesignation,
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail
        };

        await _unitOfWork.Leads.AddAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        // Ensure a School master record exists for this lead (so school info is available
        // when the FO views the lead's linked school). Schools list visibility is derived
        // from Leads at query time, so no SchoolAssignment record is needed here.
        try
        {
            var exists = await _unitOfWork.Schools.Query()
                .AnyAsync(s => s.Name == request.School
                            && (s.City ?? string.Empty) == (request.City ?? string.Empty)
                            && s.IsActive);

            if (!exists)
            {
                await _unitOfWork.Schools.AddAsync(new School
                {
                    Name = request.School,
                    City = request.City,
                    State = request.State,
                    Board = request.Board,
                    Type = request.Type,
                    StudentCount = request.Students > 0 ? request.Students : null,
                    PrincipalName = request.ContactName,
                    PrincipalPhone = request.ContactPhone,
                    Email = request.ContactEmail,
                    IsActive = true,
                });
                await _unitOfWork.SaveChangesAsync();
            }
        }
        catch { /* best-effort; don't block lead creation if school auto-create fails */ }

        // Notify assigned FO when a manager creates a lead for them
        if (assignedById.HasValue)
        {
            var creator = await _unitOfWork.Users.GetByIdAsync(creatorId);
            await _notificationService.CreateNotificationAsync(
                targetFoId,
                NotificationType.Info,
                $"New lead created: {request.School}",
                $"{creator?.Name ?? "Manager"} created and assigned a new lead to you — {request.School} ({request.City})."
            );
        }

        // Reload with navigation properties
        lead = await _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Include(l => l.AssignedBy)
            .FirstAsync(l => l.Id == lead.Id);

        return MapToLeadDto(lead);
    }

    public async Task<LeadDto?> AssignLeadAsync(int leadId, AssignLeadRequest request, int assignerId, string assignerRole)
    {
        if (assignerRole == "FO")
            throw new InvalidOperationException("Field Officers cannot assign or reassign leads");

        var lead = await _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Include(l => l.AssignedBy)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == leadId);

        if (lead == null) return null;

        // Validate target FO
        var targetFo = await _unitOfWork.Users.GetByIdAsync(request.FoId);
        if (targetFo == null || targetFo.Role != UserRole.FO)
            throw new InvalidOperationException("Target user is not a valid Field Officer");

        // Scope validation
        var assigner = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .FirstOrDefaultAsync(u => u.Id == assignerId);

        if (assigner == null)
            throw new InvalidOperationException("Assigner not found");

        if (assignerRole == "ZH" && targetFo.ZoneId != assigner.ZoneId)
            throw new InvalidOperationException("You can only assign leads to FOs in your zone");

        if (assignerRole == "RH" && targetFo.RegionId != assigner.RegionId)
            throw new InvalidOperationException("You can only assign leads to FOs in your region");

        lead.FoId = request.FoId;
        lead.AssignedById = assignerId;

        await _unitOfWork.Leads.UpdateAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        // Notify the assigned FO
        await _notificationService.CreateNotificationAsync(
            request.FoId,
            NotificationType.Info,
            $"New lead assigned: {lead.School}",
            $"{assigner.Name} assigned you a new lead — {lead.School} ({lead.City}). Check your leads page."
        );

        // Reload
        lead = await _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Include(l => l.AssignedBy)
            .Include(l => l.Activities.OrderByDescending(a => a.Date))
            .FirstAsync(l => l.Id == lead.Id);

        var school = await FindLinkedSchoolAsync(lead);
        return MapToLeadDto(lead, school);
    }

    public async Task<List<UserDto>> GetAssignableFosAsync(int userId, string userRole)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new();

        var query = _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .Where(u => u.Role == UserRole.FO);

        // Scope by role
        query = userRole switch
        {
            "ZH" => query.Where(u => u.ZoneId == user.ZoneId),
            "RH" => query.Where(u => u.RegionId == user.RegionId),
            "SH" or "SCA" => query, // can see all FOs
            "FO" => query.Where(u => u.ZoneId == user.ZoneId), // FO sees FOs in same zone
            _ => query.Where(u => u.Id == userId)
        };

        return await query
            .OrderBy(u => u.Name)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                Avatar = u.Avatar,
                ZoneId = u.ZoneId,
                Zone = u.Zone != null ? u.Zone.Name : null,
                RegionId = u.RegionId,
                Region = u.Region != null ? u.Region.Name : null,
            })
            .ToListAsync();
    }

    public async Task<LeadDto?> UpdateLeadAsync(int id, UpdateLeadRequest request, int userId)
    {
        var lead = await _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Include(l => l.AssignedBy)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lead == null) return null;

        // Authorization: FO can only edit their own leads; ZH/RH must be in the correct scope; SH/SCA always allowed
        var caller = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Id == userId);
        if (caller == null) throw new UnauthorizedAccessException("User not found");

        var authorized = caller.Role switch
        {
            UserRole.FO => lead.FoId == userId,
            UserRole.ZH => lead.Fo?.ZoneId == caller.ZoneId,
            UserRole.RH => lead.Fo?.RegionId == caller.RegionId,
            UserRole.SH or UserRole.SCA => true,
            _ => false
        };
        if (!authorized)
            throw new UnauthorizedAccessException("You don't have permission to edit this lead");

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

        LeadStage? oldStage = lead.Stage;
        if (request.Stage != null && Enum.TryParse<LeadStage>(request.Stage, true, out var stage))
            lead.Stage = stage;

        await _unitOfWork.Leads.UpdateAsync(lead);
        await _unitOfWork.SaveChangesAsync();

        // Also update the linked School record if school-level fields were provided
        var school = await FindLinkedSchoolAsync(lead);
        if (school != null)
        {
            var schoolChanged = false;
            if (request.School != null && request.School != school.Name) { school.Name = request.School; schoolChanged = true; }
            if (request.Board != null && request.Board != school.Board) { school.Board = request.Board; schoolChanged = true; }
            if (request.City != null && request.City != school.City) { school.City = request.City; schoolChanged = true; }
            if (request.State != null && request.State != school.State) { school.State = request.State; schoolChanged = true; }
            if (request.Type != null && request.Type != school.Type) { school.Type = request.Type; schoolChanged = true; }
            if (request.Students.HasValue && request.Students.Value != school.StudentCount) { school.StudentCount = request.Students.Value; schoolChanged = true; }
            if (request.SchoolAddress != null) { school.Address = request.SchoolAddress; schoolChanged = true; }
            if (request.SchoolPincode != null) { school.Pincode = request.SchoolPincode; schoolChanged = true; }
            if (request.SchoolPhone != null) { school.Phone = request.SchoolPhone; schoolChanged = true; }
            if (request.SchoolEmail != null) { school.Email = request.SchoolEmail; schoolChanged = true; }
            if (request.SchoolWebsite != null) { school.Website = request.SchoolWebsite; schoolChanged = true; }
            if (request.PrincipalName != null) { school.PrincipalName = request.PrincipalName; schoolChanged = true; }
            if (request.PrincipalPhone != null) { school.PrincipalPhone = request.PrincipalPhone; schoolChanged = true; }
            if (request.StaffCount.HasValue) { school.StaffCount = request.StaffCount.Value; schoolChanged = true; }

            if (schoolChanged)
            {
                school.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Schools.UpdateAsync(school);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        // Notify ZH on lead stage change
        if (request.Stage != null && lead.Stage != oldStage && lead.Fo?.ZoneId != null)
        {
            try
            {
                var zh = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == lead.Fo.ZoneId);
                if (zh != null)
                {
                    if (lead.Stage == LeadStage.Lost)
                        await _notificationService.CreateNotificationAsync(zh.Id, NotificationType.Warning,
                            $"Lead lost: {lead.School}", $"{lead.Fo.Name} marked {lead.School} as Lost. Reason: {lead.LossReason ?? "Not specified"}.");
                    else
                        await _notificationService.CreateNotificationAsync(zh.Id, NotificationType.Info,
                            $"Lead stage: {lead.School}", $"{lead.Fo.Name} moved {lead.School} to {lead.Stage}.");
                }
            }
            catch { }
        }

        return MapToLeadDto(lead, school);
    }

    public async Task<bool> DeleteLeadAsync(int id, int userId)
    {
        var lead = await _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null) return false;

        // Authorization — same scope rules as update
        var caller = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Id == userId);
        if (caller == null) throw new UnauthorizedAccessException("User not found");

        var authorized = caller.Role switch
        {
            UserRole.FO => lead.FoId == userId,
            UserRole.ZH => lead.Fo?.ZoneId == caller.ZoneId,
            UserRole.RH => lead.Fo?.RegionId == caller.RegionId,
            UserRole.SH or UserRole.SCA => true,
            _ => false
        };
        if (!authorized)
            throw new UnauthorizedAccessException("You don't have permission to delete this lead");

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
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return new();

        var query = _unitOfWork.Leads.Query()
            .Include(l => l.Fo)
            .Include(l => l.AssignedBy)
            .AsQueryable();

        query = user.Role switch
        {
            UserRole.FO => query.Where(l => l.FoId == userId),
            UserRole.ZH => query.Where(l => l.Fo.ZoneId == user.ZoneId),
            UserRole.RH => query.Where(l => l.Fo.RegionId == user.RegionId),
            UserRole.SH => query,
            _ => query
        };

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
                AssignedById = l.AssignedById,
                AssignedByName = l.AssignedBy != null ? l.AssignedBy.Name : null,
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

    private async Task<School?> FindLinkedSchoolAsync(Lead lead)
    {
        if (string.IsNullOrWhiteSpace(lead.School)) return null;
        return await _unitOfWork.Schools.Query()
            .FirstOrDefaultAsync(s => s.Name == lead.School
                                   && (s.City ?? string.Empty) == (lead.City ?? string.Empty)
                                   && s.IsActive);
    }

    private static LeadDto MapToLeadDto(Lead lead, School? school = null) => new()
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
        AssignedById = lead.AssignedById,
        AssignedByName = lead.AssignedBy?.Name,
        Contact = new ContactDto
        {
            Name = lead.ContactName,
            Designation = lead.ContactDesignation,
            Phone = lead.ContactPhone,
            Email = lead.ContactEmail
        },
        SchoolInfo = school == null ? null : new SchoolInfoDto
        {
            SchoolId = school.Id,
            Address = school.Address,
            Pincode = school.Pincode,
            Phone = school.Phone,
            Email = school.Email,
            Website = school.Website,
            PrincipalName = school.PrincipalName,
            PrincipalPhone = school.PrincipalPhone,
            StaffCount = school.StaffCount,
            Latitude = school.Latitude,
            Longitude = school.Longitude,
            GeofenceRadiusMetres = school.GeofenceRadiusMetres,
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
