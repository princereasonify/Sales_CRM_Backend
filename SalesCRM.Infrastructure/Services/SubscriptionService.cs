using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Subscriptions;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _uow;

    public SubscriptionService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task CreateSubscriptionFromDealAsync(int dealId)
    {
        // Check if subscription already exists for this deal
        var exists = await _uow.SchoolSubscriptions.Query().AnyAsync(s => s.DealId == dealId);
        if (exists) return;

        var deal = await _uow.Deals.Query()
            .Include(d => d.Lead)
            .FirstOrDefaultAsync(d => d.Id == dealId);
        if (deal == null) return;

        // Find the school from the lead's school name (may not exist in Schools table)
        var school = await _uow.Schools.Query()
            .FirstOrDefaultAsync(s => s.Name == deal.Lead.School && s.IsActive);
        int? schoolId = school?.Id;

        // Parse plan type from deal duration
        var planType = ParsePlanType(deal.Duration);
        var (periodStart, periodEnd) = CalculateAcademicPeriod(planType, DateTime.UtcNow);

        var subscription = new SchoolSubscription
        {
            DealId = dealId,
            SchoolId = schoolId,
            PlanType = planType,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = SubscriptionStatus.Pending,
            CredentialStatus = CredentialStatus.NotProvisioned,
            NumberOfLicenses = deal.NumberOfLicenses ?? 0,
            Modules = deal.Modules,
            Amount = deal.FinalValue,
        };

        await _uow.SchoolSubscriptions.AddAsync(subscription);
        await _uow.SaveChangesAsync();
    }

    public (DateTime start, DateTime end) CalculateAcademicPeriod(PlanType planType, DateTime dealDate)
    {
        // Academic year: June 1 to May 31
        // Regardless of when deal closes, period aligns to academic year

        var year = dealDate.Month >= 6 ? dealDate.Year : dealDate.Year - 1;

        switch (planType)
        {
            case PlanType.Annually:
                return (
                    DateTime.SpecifyKind(new DateTime(year, 6, 1), DateTimeKind.Utc),
                    DateTime.SpecifyKind(new DateTime(year + 1, 5, 31), DateTimeKind.Utc)
                );

            case PlanType.HalfYearly:
                if (dealDate.Month >= 6 && dealDate.Month <= 11)
                    return (
                        DateTime.SpecifyKind(new DateTime(year, 6, 1), DateTimeKind.Utc),
                        DateTime.SpecifyKind(new DateTime(year, 11, 30), DateTimeKind.Utc)
                    );
                else
                    return (
                        DateTime.SpecifyKind(new DateTime(year, 12, 1), DateTimeKind.Utc),
                        DateTime.SpecifyKind(new DateTime(year + 1, 5, 31), DateTimeKind.Utc)
                    );

            case PlanType.Quarterly:
                if (dealDate.Month >= 6 && dealDate.Month <= 8)
                    return (
                        DateTime.SpecifyKind(new DateTime(year, 6, 1), DateTimeKind.Utc),
                        DateTime.SpecifyKind(new DateTime(year, 8, 31), DateTimeKind.Utc)
                    );
                else if (dealDate.Month >= 9 && dealDate.Month <= 11)
                    return (
                        DateTime.SpecifyKind(new DateTime(year, 9, 1), DateTimeKind.Utc),
                        DateTime.SpecifyKind(new DateTime(year, 11, 30), DateTimeKind.Utc)
                    );
                else if (dealDate.Month == 12 || dealDate.Month <= 2)
                    return (
                        DateTime.SpecifyKind(new DateTime(dealDate.Month == 12 ? year : year, 12, 1), DateTimeKind.Utc),
                        DateTime.SpecifyKind(new DateTime(year + 1, 2, DateTime.IsLeapYear(year + 1) ? 29 : 28), DateTimeKind.Utc)
                    );
                else
                    return (
                        DateTime.SpecifyKind(new DateTime(year + 1, 3, 1), DateTimeKind.Utc),
                        DateTime.SpecifyKind(new DateTime(year + 1, 5, 31), DateTimeKind.Utc)
                    );

            case PlanType.Monthly:
                return (
                    DateTime.SpecifyKind(new DateTime(dealDate.Year, dealDate.Month, 1), DateTimeKind.Utc),
                    DateTime.SpecifyKind(new DateTime(dealDate.Year, dealDate.Month, DateTime.DaysInMonth(dealDate.Year, dealDate.Month)), DateTimeKind.Utc)
                );

            default:
                return (
                    DateTime.SpecifyKind(new DateTime(year, 6, 1), DateTimeKind.Utc),
                    DateTime.SpecifyKind(new DateTime(year + 1, 5, 31), DateTimeKind.Utc)
                );
        }
    }

    public async Task<List<SchoolSubscriptionDto>> GetSubscriptionsAsync(int requesterId, string role, string? status = null)
    {
        var query = _uow.SchoolSubscriptions.Query()
            .Include(s => s.Deal).ThenInclude(d => d.Lead)
            .Include(s => s.Deal).ThenInclude(d => d.Fo)
            .Include(s => s.School)
            .Include(s => s.CredentialProvisionedBy)
            .AsQueryable();

        // Role-based scoping
        if (role == "FO")
            query = query.Where(s => s.Deal.FoId == requesterId);
        else if (role == "ZH")
        {
            var zoneId = await _uow.Users.Query().Where(u => u.Id == requesterId).Select(u => u.ZoneId).FirstOrDefaultAsync();
            query = query.Where(s => s.Deal.Fo.ZoneId == zoneId);
        }
        else if (role == "RH")
        {
            var regionId = await _uow.Users.Query().Where(u => u.Id == requesterId).Select(u => u.RegionId).FirstOrDefaultAsync();
            query = query.Where(s => s.Deal.Fo.RegionId == regionId);
        }

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubscriptionStatus>(status, out var st))
            query = query.Where(s => s.Status == st);

        var items = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
        return items.Select(MapToDto).ToList();
    }

    public async Task<SchoolSubscriptionDto?> GetSubscriptionAsync(int id)
    {
        var sub = await _uow.SchoolSubscriptions.Query()
            .Include(s => s.Deal).ThenInclude(d => d.Lead)
            .Include(s => s.Deal).ThenInclude(d => d.Fo)
            .Include(s => s.School)
            .Include(s => s.CredentialProvisionedBy)
            .FirstOrDefaultAsync(s => s.Id == id);

        return sub == null ? null : MapToDto(sub);
    }

    public async Task<SchoolSubscriptionDto?> ProvisionCredentialsAsync(int id, ProvisionCredentialsRequest request, int provisionedById)
    {
        var sub = await _uow.SchoolSubscriptions.Query()
            .Include(s => s.Deal).ThenInclude(d => d.Lead)
            .Include(s => s.Deal).ThenInclude(d => d.Fo)
            .Include(s => s.School)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sub == null) return null;

        sub.SchoolLoginEmail = request.SchoolLoginEmail;
        sub.SchoolLoginPassword = request.SchoolLoginPassword;
        sub.CredentialStatus = CredentialStatus.Provisioned;
        sub.CredentialProvisionedAt = DateTime.UtcNow;
        sub.CredentialProvisionedById = provisionedById;
        sub.Status = SubscriptionStatus.Active;

        await _uow.SchoolSubscriptions.UpdateAsync(sub);
        await _uow.SaveChangesAsync();

        sub.CredentialProvisionedBy = await _uow.Users.GetByIdAsync(provisionedById);
        return MapToDto(sub);
    }

    public async Task<SchoolSubscriptionDto?> RevokeCredentialsAsync(int id)
    {
        var sub = await _uow.SchoolSubscriptions.Query()
            .Include(s => s.Deal).ThenInclude(d => d.Lead)
            .Include(s => s.Deal).ThenInclude(d => d.Fo)
            .Include(s => s.School)
            .Include(s => s.CredentialProvisionedBy)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sub == null) return null;

        sub.CredentialStatus = CredentialStatus.Revoked;
        sub.Status = SubscriptionStatus.Suspended;

        await _uow.SchoolSubscriptions.UpdateAsync(sub);
        await _uow.SaveChangesAsync();

        return MapToDto(sub);
    }

    public async Task<SchoolSubscriptionDto?> UpdateStatusAsync(int id, UpdateSubscriptionStatusRequest request)
    {
        var sub = await _uow.SchoolSubscriptions.Query()
            .Include(s => s.Deal).ThenInclude(d => d.Lead)
            .Include(s => s.Deal).ThenInclude(d => d.Fo)
            .Include(s => s.School)
            .Include(s => s.CredentialProvisionedBy)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sub == null) return null;

        if (Enum.TryParse<SubscriptionStatus>(request.Status, out var newStatus))
            sub.Status = newStatus;
        if (request.Notes != null)
            sub.Notes = request.Notes;

        await _uow.SchoolSubscriptions.UpdateAsync(sub);
        await _uow.SaveChangesAsync();

        return MapToDto(sub);
    }

    private static PlanType ParsePlanType(string duration)
    {
        var lower = duration.ToLowerInvariant();
        if (lower.Contains("month") && !lower.Contains("6") && !lower.Contains("half")) return PlanType.Monthly;
        if (lower.Contains("quarter") || lower.Contains("3 month")) return PlanType.Quarterly;
        if (lower.Contains("half") || lower.Contains("6 month") || lower.Contains("semi")) return PlanType.HalfYearly;
        return PlanType.Annually;
    }

    private static SchoolSubscriptionDto MapToDto(SchoolSubscription s) => new()
    {
        Id = s.Id,
        DealId = s.DealId,
        SchoolId = s.SchoolId,
        SchoolName = s.School?.Name ?? s.Deal?.Lead?.School ?? "",
        PlanType = s.PlanType.ToString(),
        PeriodStart = s.PeriodStart,
        PeriodEnd = s.PeriodEnd,
        Status = s.Status.ToString(),
        SchoolLoginEmail = s.SchoolLoginEmail,
        CredentialStatus = s.CredentialStatus.ToString(),
        CredentialProvisionedAt = s.CredentialProvisionedAt,
        CredentialProvisionedByName = s.CredentialProvisionedBy?.Name,
        NumberOfLicenses = s.NumberOfLicenses,
        Modules = s.Modules,
        Amount = s.Amount,
        Notes = s.Notes,
        DealPaymentStatus = s.Deal?.PaymentStatus,
        FoName = s.Deal?.Fo?.Name,
        DaysRemaining = Math.Max(0, (int)(s.PeriodEnd - DateTime.UtcNow).TotalDays)
    };
}
