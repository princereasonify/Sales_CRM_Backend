using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class DealService : IDealService
{
    private readonly IUnitOfWork _unitOfWork;

    public DealService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedResult<DealDto>> GetDealsAsync(int userId, PaginationParams pagination)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new PaginatedResult<DealDto>();

        var query = _unitOfWork.Deals.Query()
            .Include(d => d.Lead)
            .Include(d => d.Fo)
            .Include(d => d.Approver)
            .AsQueryable();

        query = user.Role switch
        {
            UserRole.FO => query.Where(d => d.FoId == userId),
            UserRole.ZH => query.Where(d => d.Fo.ZoneId == user.ZoneId),
            UserRole.RH => query.Where(d => d.Fo.RegionId == user.RegionId),
            _ => query
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync();

        return new PaginatedResult<DealDto>
        {
            Items = items.Select(MapToDealDto).ToList(),
            TotalCount = totalCount,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
    }

    public async Task<DealDto?> GetDealByIdAsync(int id, int userId)
    {
        var deal = await _unitOfWork.Deals.Query()
            .Include(d => d.Lead)
            .Include(d => d.Fo)
            .Include(d => d.Approver)
            .FirstOrDefaultAsync(d => d.Id == id);

        return deal == null ? null : MapToDealDto(deal);
    }

    public async Task<DealDto> CreateDealAsync(CreateDealRequest request, int foId)
    {
        var lead = await _unitOfWork.Leads.GetByIdAsync(request.LeadId)
            ?? throw new ArgumentException("Lead not found");

        var finalValue = request.ContractValue * (1 - request.Discount / 100);

        // Auto-route for approval if discount > 10%
        var approvalStatus = ApprovalStatus.Draft;
        if (request.SubmitForApproval)
        {
            approvalStatus = request.Discount > 10 ? ApprovalStatus.PendingZH : ApprovalStatus.SelfApproved;
        }

        var deal = new Deal
        {
            LeadId = request.LeadId,
            FoId = foId,
            ContractValue = request.ContractValue,
            Discount = request.Discount,
            FinalValue = finalValue,
            PaymentTerms = request.PaymentTerms,
            Duration = request.Duration,
            Modules = JsonSerializer.Serialize(request.Modules),
            Notes = request.Notes,
            ApprovalStatus = approvalStatus,
            SubmittedAt = request.SubmitForApproval ? DateTime.UtcNow : null
        };

        await _unitOfWork.Deals.AddAsync(deal);
        await _unitOfWork.SaveChangesAsync();

        deal.Lead = lead;
        deal.Fo = (await _unitOfWork.Users.GetByIdAsync(foId))!;

        return MapToDealDto(deal);
    }

    public async Task<DealDto?> ApproveDealAsync(int dealId, DealApprovalRequest request, int approverId)
    {
        var deal = await _unitOfWork.Deals.Query()
            .Include(d => d.Lead)
            .Include(d => d.Fo)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal == null) return null;

        deal.ApproverId = approverId;
        deal.ApprovalNotes = request.Notes;
        deal.ApprovalStatus = request.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;

        // If approved, update lead stage to Won
        if (request.Approved)
        {
            deal.Lead.Stage = LeadStage.Won;
            deal.Lead.Score = 95;
            await _unitOfWork.Leads.UpdateAsync(deal.Lead);
        }

        await _unitOfWork.Deals.UpdateAsync(deal);
        await _unitOfWork.SaveChangesAsync();

        deal.Approver = await _unitOfWork.Users.GetByIdAsync(approverId);
        return MapToDealDto(deal);
    }

    public async Task<List<DealDto>> GetPendingApprovalsAsync(int zhId)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .FirstOrDefaultAsync(u => u.Id == zhId);

        if (user == null) return new();

        var deals = await _unitOfWork.Deals.Query()
            .Include(d => d.Lead)
            .Include(d => d.Fo)
            .Where(d => d.ApprovalStatus == ApprovalStatus.PendingZH && d.Fo.ZoneId == user.ZoneId)
            .OrderBy(d => d.SubmittedAt)
            .ToListAsync();

        return deals.Select(MapToDealDto).ToList();
    }

    private static DealDto MapToDealDto(Deal deal) => new()
    {
        Id = deal.Id,
        LeadId = deal.LeadId,
        School = deal.Lead?.School ?? string.Empty,
        FoId = deal.FoId,
        FoName = deal.Fo?.Name ?? string.Empty,
        ContractValue = deal.ContractValue,
        Discount = deal.Discount,
        FinalValue = deal.FinalValue,
        PaymentTerms = deal.PaymentTerms,
        Duration = deal.Duration,
        Modules = DeserializeModules(deal.Modules),
        Notes = deal.Notes,
        ApprovalStatus = deal.ApprovalStatus.ToString(),
        SubmittedAt = deal.SubmittedAt,
        ApproverName = deal.Approver?.Name,
        ApprovalNotes = deal.ApprovalNotes,
        Students = deal.Lead?.Students ?? 0
    };

    private static List<string> DeserializeModules(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }
}
