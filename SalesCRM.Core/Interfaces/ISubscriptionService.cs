using SalesCRM.Core.DTOs.Subscriptions;
using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Interfaces;

public interface ISubscriptionService
{
    Task CreateSubscriptionFromDealAsync(int dealId);
    Task<List<SchoolSubscriptionDto>> GetSubscriptionsAsync(int requesterId, string role, string? status = null);
    Task<SchoolSubscriptionDto?> GetSubscriptionAsync(int id);
    Task<SchoolSubscriptionDto?> ProvisionCredentialsAsync(int id, ProvisionCredentialsRequest request, int provisionedById);
    Task<SchoolSubscriptionDto?> RevokeCredentialsAsync(int id);
    Task<SchoolSubscriptionDto?> UpdateStatusAsync(int id, UpdateSubscriptionStatusRequest request);
    (DateTime start, DateTime end) CalculateAcademicPeriod(PlanType planType, DateTime dealDate);
}
