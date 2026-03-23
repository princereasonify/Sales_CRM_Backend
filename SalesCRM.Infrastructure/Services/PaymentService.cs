using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Payments;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    public PaymentService(IUnitOfWork uow) => _uow = uow;

    public async Task<(List<PaymentDto> Payments, int Total)> GetPaymentsAsync(int? dealId, string? status, int page, int limit)
    {
        var q = _uow.Payments.Query()
            .Include(p => p.Deal).ThenInclude(d => d.Lead)
            .Include(p => p.School).Include(p => p.CollectedBy).Include(p => p.VerifiedBy)
            .AsQueryable();

        if (dealId.HasValue) q = q.Where(p => p.DealId == dealId.Value);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentStatus>(status, true, out var s))
            q = q.Where(p => p.Status == s);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * limit).Take(limit).ToListAsync();

        return (items.Select(p => new PaymentDto
        {
            Id = p.Id, DealId = p.DealId, DealName = p.Deal?.Lead?.School,
            SchoolId = p.SchoolId, SchoolName = p.School?.Name,
            Amount = p.Amount, Method = p.Method.ToString(), Status = p.Status.ToString(),
            TransactionId = p.TransactionId, ChequeNumber = p.ChequeNumber,
            ChequeImageUrl = p.ChequeImageUrl, BankName = p.BankName, UpiId = p.UpiId,
            ReceiptUrl = p.ReceiptUrl, Notes = p.Notes,
            CollectedByName = p.CollectedBy?.Name, VerifiedByName = p.VerifiedBy?.Name,
            VerifiedAt = p.VerifiedAt, CreatedAt = p.CreatedAt
        }).ToList(), total);
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, int collectedById)
    {
        Enum.TryParse<PaymentMethod>(request.Method, true, out var method);
        var payment = new Payment
        {
            DealId = request.DealId, SchoolId = request.SchoolId, Amount = request.Amount,
            Method = method, TransactionId = request.TransactionId,
            ChequeNumber = request.ChequeNumber, ChequeImageUrl = request.ChequeImageUrl,
            BankName = request.BankName, UpiId = request.UpiId, Notes = request.Notes,
            CollectedById = collectedById, Status = PaymentStatus.Pending
        };
        await _uow.Payments.AddAsync(payment);
        await _uow.SaveChangesAsync();

        var (list, _) = await GetPaymentsAsync(null, null, 1, 1);
        return list.FirstOrDefault(p => p.Id == payment.Id) ?? new PaymentDto { Id = payment.Id };
    }

    public async Task<PaymentDto?> VerifyPaymentAsync(int id, VerifyPaymentRequest request, int verifiedById)
    {
        var p = await _uow.Payments.GetByIdAsync(id);
        if (p == null) return null;
        p.Status = request.Verified ? PaymentStatus.Completed : PaymentStatus.Failed;
        p.VerifiedById = verifiedById;
        p.VerifiedAt = DateTime.UtcNow;
        if (request.Notes != null) p.Notes = request.Notes;
        await _uow.SaveChangesAsync();

        var (list, _) = await GetPaymentsAsync(null, null, 1, 100);
        return list.FirstOrDefault(x => x.Id == id);
    }
}
