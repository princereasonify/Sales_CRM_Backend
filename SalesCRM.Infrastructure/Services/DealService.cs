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
    private readonly INotificationService _notificationService;
    private readonly ISubscriptionService _subscriptionService;

    public DealService(IUnitOfWork unitOfWork, INotificationService notificationService, ISubscriptionService subscriptionService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _subscriptionService = subscriptionService;
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
        if (request.LeadId <= 0)
            throw new ArgumentException("A valid lead is required to create a deal");

        var lead = await _unitOfWork.Leads.GetByIdAsync(request.LeadId)
            ?? throw new ArgumentException("Lead not found");

        // Ownership check — only the FO who owns the lead (or managers in scope) can create a deal on it
        var caller = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == foId)
            ?? throw new UnauthorizedAccessException("User not found");

        var leadFo = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == lead.FoId);

        var allowed = caller.Role switch
        {
            UserRole.FO => lead.FoId == foId,
            UserRole.ZH => leadFo?.ZoneId == caller.ZoneId,
            UserRole.RH => leadFo?.RegionId == caller.RegionId,
            UserRole.SH or UserRole.SCA => true,
            _ => false
        };
        if (!allowed)
            throw new UnauthorizedAccessException("You can only create deals on leads in your scope");

        // GST Calculation Engine
        var basePrice = request.BasePrice;
        var totalLogins = request.TotalLogins;
        var subtotal = basePrice * totalLogins;
        var amountWithoutGst = subtotal * (1 - request.Discount / 100);
        var gstAmount = amountWithoutGst * 0.18m;
        var totalMoney = amountWithoutGst + gstAmount;
        var contractValue = request.ContractValue > 0 ? request.ContractValue : subtotal;
        var finalValue = totalMoney;

        // Auto-route for approval based on discount
        var approvalStatus = ApprovalStatus.Draft;
        if (request.SubmitForApproval)
        {
            approvalStatus = request.Discount switch
            {
                <= 10 => ApprovalStatus.SelfApproved,
                <= 20 => ApprovalStatus.PendingZH,
                <= 30 => ApprovalStatus.PendingRH,
                _ => ApprovalStatus.PendingSH
            };
        }

        var deal = new Deal
        {
            LeadId = request.LeadId,
            FoId = foId,
            ContractValue = contractValue,
            Discount = request.Discount,
            FinalValue = finalValue,
            BasePrice = basePrice,
            TotalLogins = totalLogins,
            Subtotal = subtotal,
            AmountWithoutGst = amountWithoutGst,
            GstAmount = gstAmount,
            TotalMoney = totalMoney,
            BillingFrequency = request.BillingFrequency,
            OnboardingDate = request.OnboardingDate,
            PaymentTerms = request.PaymentTerms,
            Duration = request.Duration,
            Modules = JsonSerializer.Serialize(request.Modules),
            Notes = request.Notes,
            ApprovalStatus = approvalStatus,
            SubmittedAt = request.SubmitForApproval ? DateTime.UtcNow : null,
            ContractStartDate = request.ContractStartDate,
            ContractEndDate = request.ContractEndDate,
            NumberOfLicenses = request.NumberOfLicenses,
            PaymentStatus = request.PaymentStatus ?? "Pending"
        };

        await _unitOfWork.Deals.AddAsync(deal);
        await _unitOfWork.SaveChangesAsync();

        deal.Lead = lead;
        deal.Fo = (await _unitOfWork.Users.GetByIdAsync(foId))!;

        // Notify self-approved
        if (approvalStatus == ApprovalStatus.SelfApproved)
        {
            await _notificationService.CreateNotificationAsync(foId, NotificationType.Success,
                $"Deal auto-approved: {lead.School}", $"Your deal for {lead.School} (₹{deal.TotalMoney:N0}) was auto-approved (discount ≤10%).");
        }

        // Notify approver based on discount routing
        if (approvalStatus == ApprovalStatus.PendingZH && deal.Fo.ZoneId != null)
        {
            var zh = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == deal.Fo.ZoneId);
            if (zh != null)
                await _notificationService.CreateNotificationAsync(zh.Id, NotificationType.Urgent,
                    $"Deal pending approval: {lead.School}", $"{deal.Fo.Name} submitted a deal for {lead.School} (₹{deal.TotalMoney:N0}, {deal.Discount}% off). Needs your approval.");
        }
        if (approvalStatus == ApprovalStatus.PendingRH)
        {
            var foWithRegion = await _unitOfWork.Users.Query().Include(u => u.Zone).FirstOrDefaultAsync(u => u.Id == foId);
            var regionId = foWithRegion?.RegionId ?? foWithRegion?.Zone?.RegionId;
            if (regionId != null)
            {
                var rh = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.RH && u.RegionId == regionId);
                if (rh != null)
                    await _notificationService.CreateNotificationAsync(rh.Id, NotificationType.Urgent,
                        $"Deal pending approval: {lead.School}", $"{deal.Fo.Name} submitted a deal for {lead.School} (₹{deal.TotalMoney:N0}, {deal.Discount}% off). Needs RH approval.");
            }
        }
        if (approvalStatus == ApprovalStatus.PendingSH)
        {
            var sh = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.SH);
            if (sh != null)
                await _notificationService.CreateNotificationAsync(sh.Id, NotificationType.Urgent,
                    $"Deal pending approval: {lead.School}", $"{deal.Fo.Name} submitted a deal for {lead.School} (₹{deal.TotalMoney:N0}, {deal.Discount}% off). Needs SH approval.");
        }

        return MapToDealDto(deal);
    }

    public async Task<DealDto?> ApproveDealAsync(int dealId, DealApprovalRequest request, int approverId)
    {
        var deal = await _unitOfWork.Deals.Query()
            .Include(d => d.Lead)
            .Include(d => d.Fo)
            .FirstOrDefaultAsync(d => d.Id == dealId);

        if (deal == null) return null;

        // Only leads in Pending* states can be approved/rejected (prevents double-approve / rejecting Draft)
        if (deal.ApprovalStatus != ApprovalStatus.PendingZH
            && deal.ApprovalStatus != ApprovalStatus.PendingRH
            && deal.ApprovalStatus != ApprovalStatus.PendingSH)
            throw new InvalidOperationException($"Deal is not awaiting approval (current status: {deal.ApprovalStatus})");

        // Approver must be the correct role for the pending status + in correct scope
        var approver = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == approverId)
            ?? throw new UnauthorizedAccessException("Approver not found");

        // FOs can never approve deals (even self-submitted)
        if (approver.Role == UserRole.FO)
            throw new UnauthorizedAccessException("Field Officers cannot approve deals");

        // Role must match the pending level (SH/SCA can override any pending level)
        bool roleMatches = deal.ApprovalStatus switch
        {
            ApprovalStatus.PendingZH => approver.Role == UserRole.ZH || approver.Role == UserRole.RH || approver.Role == UserRole.SH || approver.Role == UserRole.SCA,
            ApprovalStatus.PendingRH => approver.Role == UserRole.RH || approver.Role == UserRole.SH || approver.Role == UserRole.SCA,
            ApprovalStatus.PendingSH => approver.Role == UserRole.SH || approver.Role == UserRole.SCA,
            _ => false
        };
        if (!roleMatches)
            throw new UnauthorizedAccessException($"Your role ({approver.Role}) cannot approve a deal requiring {deal.ApprovalStatus}");

        // Scope check — ZH must be in the same zone, RH in same region
        if (approver.Role == UserRole.ZH && deal.Fo?.ZoneId != approver.ZoneId)
            throw new UnauthorizedAccessException("You can only approve deals from your zone");
        if (approver.Role == UserRole.RH && deal.Fo?.RegionId != approver.RegionId)
            throw new UnauthorizedAccessException("You can only approve deals from your region");

        deal.ApproverId = approverId;
        deal.ApprovalNotes = request.Notes;
        deal.ApprovalStatus = request.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;

        // If approved, update lead stage to Won and create subscription
        if (request.Approved)
        {
            deal.Lead.Stage = LeadStage.Won;
            deal.Lead.Score = 95;
            await _unitOfWork.Leads.UpdateAsync(deal.Lead);
        }

        await _unitOfWork.Deals.UpdateAsync(deal);
        await _unitOfWork.SaveChangesAsync();

        // Auto-create school subscription when deal is approved
        if (request.Approved)
        {
            try { await _subscriptionService.CreateSubscriptionFromDealAsync(deal.Id); }
            catch { /* Don't fail deal approval if subscription creation has issues */ }
        }

        // Notify FO about approval/rejection
        var school = deal.Lead?.School ?? "Unknown";
        if (request.Approved)
        {
            await _notificationService.CreateNotificationAsync(
                deal.FoId,
                NotificationType.Success,
                $"Deal approved: {school}",
                $"Your deal for {school} has been approved by {approver?.Name ?? "Manager"}. Congratulations!"
            );
        }
        else
        {
            await _notificationService.CreateNotificationAsync(
                deal.FoId,
                NotificationType.Warning,
                $"Deal rejected: {school}",
                $"Your deal for {school} was rejected by {approver?.Name ?? "Manager"}. Reason: {request.Notes ?? "No reason provided"}."
            );
        }

        deal.Approver = approver;
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
        Students = deal.Lead?.Students ?? 0,
        ContractStartDate = deal.ContractStartDate,
        ContractEndDate = deal.ContractEndDate,
        NumberOfLicenses = deal.NumberOfLicenses,
        PaymentStatus = deal.PaymentStatus,
        ContractPdfUrl = deal.ContractPdfUrl,
        BasePrice = deal.BasePrice,
        TotalLogins = deal.TotalLogins,
        Subtotal = deal.Subtotal,
        AmountWithoutGst = deal.AmountWithoutGst,
        GstAmount = deal.GstAmount,
        TotalMoney = deal.TotalMoney,
        BillingFrequency = deal.BillingFrequency,
        OnboardingDate = deal.OnboardingDate
    };

    private static List<string> DeserializeModules(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }
}
