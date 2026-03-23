using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Payments;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class PaymentsController : BaseApiController
{
    private readonly IPaymentService _svc;
    public PaymentsController(IPaymentService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetPayments([FromQuery] int? dealId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var (payments, total) = await _svc.GetPaymentsAsync(dealId, status, page, limit);
        return Ok(ApiResponse<object>.Ok(new { payments, total, page, limit }));
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var payment = await _svc.CreatePaymentAsync(request, UserId);
        return Ok(ApiResponse<PaymentDto>.Ok(payment));
    }

    [HttpPatch("{id}/verify")]
    public async Task<IActionResult> VerifyPayment(int id, [FromBody] VerifyPaymentRequest request)
    {
        var payment = await _svc.VerifyPaymentAsync(id, request, UserId);
        if (payment == null) return NotFound(ApiResponse<PaymentDto>.Fail("Payment not found"));
        return Ok(ApiResponse<PaymentDto>.Ok(payment));
    }
}
