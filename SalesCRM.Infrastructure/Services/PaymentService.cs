using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Payments;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notify;
    public PaymentService(IUnitOfWork uow, INotificationService notify) { _uow = uow; _notify = notify; }

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

        // Notify SCA/SH about new payment
        try
        {
            var scaUsers = await _uow.Users.Query().Where(u => (u.Role == UserRole.SCA || u.Role == UserRole.SH) && u.IsActive).ToListAsync();
            foreach (var admin in scaUsers)
                await _notify.CreateNotificationAsync(admin.Id, NotificationType.Info, "Payment recorded", $"A payment of ₹{request.Amount:N0} has been recorded.");
        }
        catch { }

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

        // Notify the FO who collected the payment
        try
        {
            if (request.Verified)
                await _notify.CreateNotificationAsync(p.CollectedById, NotificationType.Success, "Payment verified", $"Your payment of ₹{p.Amount:N0} has been verified.");
            else
                await _notify.CreateNotificationAsync(p.CollectedById, NotificationType.Warning, "Payment rejected", $"Your payment of ₹{p.Amount:N0} was rejected. {request.Notes ?? ""}");
        }
        catch { }

        var (list, _) = await GetPaymentsAsync(null, null, 1, 100);
        return list.FirstOrDefault(x => x.Id == id);
    }

    public async Task<List<DirectPaymentDto>> GetDirectPaymentsAsync()
    {
        var payments = await _uow.DirectPayments.Query()
            .Include(p => p.Recipient)
            .Include(p => p.PaidBy)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(p => new DirectPaymentDto
        {
            Id = p.Id,
            RecipientId = p.RecipientId,
            RecipientName = p.Recipient?.Name ?? "",
            RecipientRole = p.Recipient?.Role.ToString() ?? "",
            Amount = p.Amount,
            Method = p.Method.ToString(),
            Status = p.Status.ToString(),
            TransactionId = p.TransactionId,
            UpiId = p.UpiId,
            BankName = p.BankName,
            Notes = p.Notes,
            Purpose = p.Purpose,
            PaidByName = p.PaidBy?.Name ?? "",
            CreatedAt = p.CreatedAt
        }).ToList();
    }

    public async Task<DirectPaymentDto> CreateDirectPaymentAsync(CreateDirectPaymentRequest request, int paidById)
    {
        Enum.TryParse<PaymentMethod>(request.Method, true, out var method);
        var payment = new DirectPayment
        {
            RecipientId = request.RecipientId,
            Amount = request.Amount,
            Method = method,
            TransactionId = request.TransactionId,
            UpiId = request.UpiId,
            BankName = request.BankName,
            Notes = request.Notes,
            Purpose = request.Purpose,
            PaidById = paidById,
            Status = PaymentStatus.Completed
        };
        await _uow.DirectPayments.AddAsync(payment);
        await _uow.SaveChangesAsync();

        var saved = await _uow.DirectPayments.Query()
            .Include(p => p.Recipient)
            .Include(p => p.PaidBy)
            .FirstAsync(p => p.Id == payment.Id);

        return new DirectPaymentDto
        {
            Id = saved.Id,
            RecipientId = saved.RecipientId,
            RecipientName = saved.Recipient?.Name ?? "",
            RecipientRole = saved.Recipient?.Role.ToString() ?? "",
            Amount = saved.Amount,
            Method = saved.Method.ToString(),
            Status = saved.Status.ToString(),
            TransactionId = saved.TransactionId,
            UpiId = saved.UpiId,
            BankName = saved.BankName,
            Notes = saved.Notes,
            Purpose = saved.Purpose,
            PaidByName = saved.PaidBy?.Name ?? "",
            CreatedAt = saved.CreatedAt
        };
    }
}
