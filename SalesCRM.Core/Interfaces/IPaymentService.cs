using SalesCRM.Core.DTOs.Payments;

namespace SalesCRM.Core.Interfaces;

public interface IPaymentService
{
    Task<List<EligibleSchoolDto>> GetEligibleSchoolsAsync(int userId, string userRole);
    Task<(List<PaymentLinkDto> Items, int Total)> GetPaymentLinksAsync(int userId, string userRole, int? schoolId, string? status, int page, int limit);
    Task<PaymentLinkDto?> GetPaymentLinkByIdAsync(int id, int userId, string userRole);
    Task<(PaymentLinkDto? Link, string? Error)> CreatePaymentLinkAsync(CreatePaymentLinkRequest request, int userId, string userRole);
    Task<(PaymentLinkDto? Link, string? Error)> RefreshStatusAsync(int id, int userId, string userRole);
    Task<bool> ProcessWebhookAsync(string rawBody);
}
