using SalesCRM.Core.DTOs.Payments;

namespace SalesCRM.Core.Interfaces;

public interface IPaymentService
{
    Task<(List<PaymentDto> Payments, int Total)> GetPaymentsAsync(int? dealId, string? status, int page, int limit);
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, int collectedById);
    Task<PaymentDto?> VerifyPaymentAsync(int id, VerifyPaymentRequest request, int verifiedById);
}
