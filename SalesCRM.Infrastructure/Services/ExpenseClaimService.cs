using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Expense;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class ExpenseClaimService : IExpenseClaimService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notify;

    public ExpenseClaimService(IUnitOfWork uow, INotificationService notify)
    {
        _uow = uow;
        _notify = notify;
    }

    public async Task<ExpenseClaimDto> CreateClaimAsync(CreateExpenseClaimRequest request, string? billUrl, int userId)
    {
        var user = await _uow.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new Exception("User not found");

        if (!Enum.TryParse<ExpenseCategory>(request.Category, true, out var category))
            throw new Exception("Invalid expense category");

        var claim = new ExpenseClaim
        {
            UserId = userId,
            ExpenseDate = DateTime.SpecifyKind(request.ExpenseDate.Date, DateTimeKind.Utc),
            Category = category,
            Amount = request.Amount,
            Description = request.Description,
            BillUrl = billUrl,
            Status = ExpenseClaimStatus.Pending
        };

        await _uow.ExpenseClaims.AddAsync(claim);
        await _uow.SaveChangesAsync();

        // Notify superior
        var superiorId = await GetSuperiorIdAsync(user);
        if (superiorId.HasValue)
        {
            await _notify.CreateNotificationAsync(superiorId.Value, NotificationType.Info,
                "New Expense Claim",
                $"{user.Name} submitted a {category} expense claim of \u20B9{request.Amount:N0} on {claim.ExpenseDate:dd MMM yyyy}.");
        }

        return await GetByIdAsync(claim.Id) ?? throw new Exception("Failed to create claim");
    }

    public async Task<List<ExpenseClaimDto>> GetMyClaimsAsync(int userId, string? status, string? category, DateTime? from, DateTime? to)
    {
        var query = _uow.ExpenseClaims.Query()
            .Include(e => e.User).Include(e => e.ActionedBy)
            .Where(e => e.UserId == userId);

        query = ApplyFilters(query, status, category, from, to);

        return await query.OrderByDescending(e => e.ExpenseDate)
            .Select(e => ToDto(e)).ToListAsync();
    }

    public async Task<List<ExpenseClaimDto>> GetTeamClaimsAsync(int managerId, string role, string? status, string? category, DateTime? from, DateTime? to)
    {
        var manager = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == managerId);
        if (manager == null) return new();

        var query = _uow.ExpenseClaims.Query()
            .Include(e => e.User).Include(e => e.ActionedBy)
            .AsQueryable();

        query = role switch
        {
            "ZH" => query.Where(e => e.User.Role == UserRole.FO && e.User.ZoneId == manager.ZoneId),
            "RH" => query.Where(e => e.User.Role == UserRole.ZH && e.User.RegionId == manager.RegionId),
            "SH" => query.Where(e => e.User.Role == UserRole.RH),
            "SCA" => query,
            _ => query.Where(e => false)
        };

        query = ApplyFilters(query, status, category, from, to);

        return await query.OrderByDescending(e => e.ExpenseDate)
            .Select(e => ToDto(e)).ToListAsync();
    }

    public async Task<ExpenseClaimDto?> ApproveClaimAsync(int id, int approverId)
    {
        var claim = await _uow.ExpenseClaims.Query()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (claim == null || claim.Status != ExpenseClaimStatus.Pending) return null;

        // Prevent self-approval
        if (claim.UserId == approverId)
            throw new UnauthorizedAccessException("You cannot approve your own expense claim");

        claim.Status = ExpenseClaimStatus.Approved;
        claim.ActionedById = approverId;
        claim.ActionedAt = DateTime.UtcNow;
        await _uow.ExpenseClaims.UpdateAsync(claim);
        await _uow.SaveChangesAsync();

        var approver = await _uow.Users.GetByIdAsync(approverId);
        await _notify.CreateNotificationAsync(claim.UserId, NotificationType.Success,
            "Expense Claim Approved",
            $"Your {claim.Category} expense claim of \u20B9{claim.Amount:N0} has been approved by {approver?.Name}.");

        return await GetByIdAsync(id);
    }

    public async Task<ExpenseClaimDto?> RejectClaimAsync(int id, RejectExpenseClaimRequest request, int approverId)
    {
        var claim = await _uow.ExpenseClaims.Query()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (claim == null || claim.Status != ExpenseClaimStatus.Pending) return null;

        // Prevent self-rejection
        if (claim.UserId == approverId)
            throw new UnauthorizedAccessException("You cannot reject your own expense claim");

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
            throw new InvalidOperationException("Rejection reason is required");

        claim.Status = ExpenseClaimStatus.Rejected;
        claim.ActionedById = approverId;
        claim.ActionedAt = DateTime.UtcNow;
        claim.RejectionReason = request.RejectionReason;
        await _uow.ExpenseClaims.UpdateAsync(claim);
        await _uow.SaveChangesAsync();

        var approver = await _uow.Users.GetByIdAsync(approverId);
        await _notify.CreateNotificationAsync(claim.UserId, NotificationType.Warning,
            "Expense Claim Rejected",
            $"Your {claim.Category} expense claim of \u20B9{claim.Amount:N0} was rejected by {approver?.Name}. Reason: {request.RejectionReason}");

        return await GetByIdAsync(id);
    }

    public async Task<int> BulkApproveClaimsAsync(List<int> ids, int approverId)
    {
        var claims = await _uow.ExpenseClaims.Query()
            .Where(e => ids.Contains(e.Id) && e.Status == ExpenseClaimStatus.Pending)
            .ToListAsync();

        foreach (var c in claims)
        {
            c.Status = ExpenseClaimStatus.Approved;
            c.ActionedById = approverId;
            c.ActionedAt = DateTime.UtcNow;
            await _uow.ExpenseClaims.UpdateAsync(c);
        }

        await _uow.SaveChangesAsync();
        return claims.Count;
    }

    private async Task<ExpenseClaimDto?> GetByIdAsync(int id)
    {
        var e = await _uow.ExpenseClaims.Query()
            .Include(x => x.User).Include(x => x.ActionedBy)
            .FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? null : ToDto(e);
    }

    private async Task<int?> GetSuperiorIdAsync(User user)
    {
        return user.Role switch
        {
            UserRole.FO => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.ZH && u.ZoneId == user.ZoneId))?.Id,
            UserRole.ZH => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.RH && u.RegionId == user.RegionId))?.Id,
            UserRole.RH => (await _uow.Users.Query().FirstOrDefaultAsync(u => u.Role == UserRole.SH))?.Id,
            _ => null
        };
    }

    private static IQueryable<ExpenseClaim> ApplyFilters(IQueryable<ExpenseClaim> query, string? status, string? category, DateTime? from, DateTime? to)
    {
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ExpenseClaimStatus>(status, true, out var s))
            query = query.Where(e => e.Status == s);
        if (!string.IsNullOrEmpty(category) && Enum.TryParse<ExpenseCategory>(category, true, out var c))
            query = query.Where(e => e.Category == c);
        if (from.HasValue)
            query = query.Where(e => e.ExpenseDate >= DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc));
        if (to.HasValue)
            query = query.Where(e => e.ExpenseDate <= DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc));
        return query;
    }

    private static ExpenseClaimDto ToDto(ExpenseClaim e) => new()
    {
        Id = e.Id,
        UserId = e.UserId,
        UserName = e.User?.Name ?? "",
        UserRole = e.User?.Role.ToString() ?? "",
        ExpenseDate = e.ExpenseDate,
        Category = e.Category.ToString(),
        Amount = e.Amount,
        Description = e.Description,
        BillUrl = e.BillUrl,
        Status = e.Status.ToString(),
        ActionedById = e.ActionedById,
        ActionedByName = e.ActionedBy?.Name,
        ActionedAt = e.ActionedAt,
        RejectionReason = e.RejectionReason,
        CreatedAt = e.CreatedAt
    };
}
