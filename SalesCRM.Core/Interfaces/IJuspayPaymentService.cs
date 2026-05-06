namespace SalesCRM.Core.Interfaces;

public record JuspaySessionResult(
    bool Success,
    string? PaymentUrl,
    string? JuspayOrderRef,
    DateTime? ExpiryAtUtc,
    string RawJson,
    int HttpStatus);

public record JuspayOrderStatusResult(
    bool Success,
    string OrderId,
    string? Status,
    string RawJson,
    string? ErrorBody,
    int HttpStatus);

public interface IJuspayPaymentService
{
    Task<JuspaySessionResult> CreateSessionAsync(
        string orderId,
        decimal amount,
        int schoolId,
        int? schoolAdminUserId,
        string? customerEmail,
        string? customerPhone,
        CancellationToken ct = default);

    Task<JuspayOrderStatusResult> GetOrderStatusAsync(string orderId, CancellationToken ct = default);
}
