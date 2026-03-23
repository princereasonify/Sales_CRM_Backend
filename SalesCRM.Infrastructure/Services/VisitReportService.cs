using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.VisitReports;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class VisitReportService : IVisitReportService
{
    private readonly IUnitOfWork _uow;
    public VisitReportService(IUnitOfWork uow) => _uow = uow;

    public async Task<VisitReportDto> CreateVisitReportAsync(CreateVisitReportRequest request, int userId)
    {
        Enum.TryParse<VisitPurpose>(request.Purpose, true, out var purpose);
        Enum.TryParse<NextActionType>(request.NextAction, true, out var nextAction);

        var report = new VisitReport
        {
            SchoolVisitLogId = request.SchoolVisitLogId, ActivityId = request.ActivityId,
            UserId = userId, SchoolId = request.SchoolId, Purpose = purpose,
            PersonMetId = request.PersonMetId, Outcome = request.Outcome, Remarks = request.Remarks,
            NextAction = nextAction,
            NextActionDate = request.NextActionDate.HasValue ? DateTime.SpecifyKind(request.NextActionDate.Value, DateTimeKind.Utc) : null,
            NextActionNotes = request.NextActionNotes, CustomFields = request.CustomFields, Photos = request.Photos
        };
        await _uow.VisitReports.AddAsync(report);
        await _uow.SaveChangesAsync();

        return await MapToDto(report.Id);
    }

    public async Task<List<VisitReportDto>> GetVisitReportsByUserAsync(int userId, string? date)
    {
        var q = _uow.VisitReports.Query()
            .Include(v => v.User).Include(v => v.School).Include(v => v.PersonMet)
            .Where(v => v.UserId == userId);

        if (DateTime.TryParse(date, out var d))
            q = q.Where(v => v.CreatedAt.Date == d.Date);

        var items = await q.OrderByDescending(v => v.CreatedAt).Take(100).ToListAsync();
        return items.Select(v => new VisitReportDto
        {
            Id = v.Id, SchoolVisitLogId = v.SchoolVisitLogId, ActivityId = v.ActivityId,
            UserId = v.UserId, UserName = v.User?.Name, SchoolId = v.SchoolId, SchoolName = v.School?.Name,
            Purpose = v.Purpose.ToString(), PersonMetId = v.PersonMetId, PersonMetName = v.PersonMet?.Name,
            Outcome = v.Outcome, Remarks = v.Remarks, NextAction = v.NextAction.ToString(),
            NextActionDate = v.NextActionDate, NextActionNotes = v.NextActionNotes,
            CustomFields = v.CustomFields, Photos = v.Photos, CreatedAt = v.CreatedAt
        }).ToList();
    }

    private async Task<VisitReportDto> MapToDto(int id)
    {
        var v = await _uow.VisitReports.Query()
            .Include(x => x.User).Include(x => x.School).Include(x => x.PersonMet)
            .FirstAsync(x => x.Id == id);
        return new VisitReportDto
        {
            Id = v.Id, SchoolVisitLogId = v.SchoolVisitLogId, ActivityId = v.ActivityId,
            UserId = v.UserId, UserName = v.User?.Name, SchoolId = v.SchoolId, SchoolName = v.School?.Name,
            Purpose = v.Purpose.ToString(), PersonMetId = v.PersonMetId, PersonMetName = v.PersonMet?.Name,
            Outcome = v.Outcome, Remarks = v.Remarks, NextAction = v.NextAction.ToString(),
            NextActionDate = v.NextActionDate, NextActionNotes = v.NextActionNotes,
            CustomFields = v.CustomFields, Photos = v.Photos, CreatedAt = v.CreatedAt
        };
    }

    // ─── Visit Field Configs ──────────────────────────────────────────────────

    public async Task<List<VisitFieldConfigDto>> GetVisitFieldConfigsAsync()
    {
        return await _uow.VisitFieldConfigs.Query()
            .Where(f => f.IsActive)
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new VisitFieldConfigDto
            {
                Id = f.Id, FieldName = f.FieldName, FieldType = f.FieldType,
                Options = f.Options, IsRequired = f.IsRequired, IsActive = f.IsActive, DisplayOrder = f.DisplayOrder
            })
            .ToListAsync();
    }

    public async Task<VisitFieldConfigDto> CreateVisitFieldConfigAsync(CreateVisitFieldConfigRequest request, int createdById)
    {
        var config = new VisitFieldConfig
        {
            FieldName = request.FieldName, FieldType = request.FieldType,
            Options = request.Options, IsRequired = request.IsRequired,
            DisplayOrder = request.DisplayOrder, CreatedById = createdById
        };
        await _uow.VisitFieldConfigs.AddAsync(config);
        await _uow.SaveChangesAsync();
        return new VisitFieldConfigDto
        {
            Id = config.Id, FieldName = config.FieldName, FieldType = config.FieldType,
            Options = config.Options, IsRequired = config.IsRequired, IsActive = config.IsActive, DisplayOrder = config.DisplayOrder
        };
    }

    public async Task<VisitFieldConfigDto?> UpdateVisitFieldConfigAsync(int id, CreateVisitFieldConfigRequest request)
    {
        var f = await _uow.VisitFieldConfigs.GetByIdAsync(id);
        if (f == null) return null;
        f.FieldName = request.FieldName; f.FieldType = request.FieldType;
        f.Options = request.Options; f.IsRequired = request.IsRequired; f.DisplayOrder = request.DisplayOrder;
        await _uow.SaveChangesAsync();
        return new VisitFieldConfigDto
        {
            Id = f.Id, FieldName = f.FieldName, FieldType = f.FieldType,
            Options = f.Options, IsRequired = f.IsRequired, IsActive = f.IsActive, DisplayOrder = f.DisplayOrder
        };
    }

    public async Task<bool> DeleteVisitFieldConfigAsync(int id)
    {
        var f = await _uow.VisitFieldConfigs.GetByIdAsync(id);
        if (f == null) return false;
        f.IsActive = false;
        await _uow.SaveChangesAsync();
        return true;
    }
}
