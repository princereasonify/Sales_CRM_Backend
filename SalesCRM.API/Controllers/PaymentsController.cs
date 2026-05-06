using System.IO;
using Microsoft.AspNetCore.Authorization;
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

    [HttpGet("eligible-schools")]
    public async Task<IActionResult> GetEligibleSchools()
    {
        var schools = await _svc.GetEligibleSchoolsAsync(UserId, UserRole);
        return Ok(ApiResponse<List<EligibleSchoolDto>>.Ok(schools));
    }

    [HttpGet("links")]
    public async Task<IActionResult> GetLinks([FromQuery] int? schoolId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var (items, total) = await _svc.GetPaymentLinksAsync(UserId, UserRole, schoolId, status, page, limit);
        return Ok(ApiResponse<object>.Ok(new { items, total, page, limit }));
    }

    [HttpGet("links/{id:int}")]
    public async Task<IActionResult> GetLink(int id)
    {
        var item = await _svc.GetPaymentLinkByIdAsync(id, UserId, UserRole);
        if (item == null) return NotFound(ApiResponse<PaymentLinkDto>.Fail("Payment link not found"));
        return Ok(ApiResponse<PaymentLinkDto>.Ok(item));
    }

    [HttpPost("links")]
    public async Task<IActionResult> CreateLink([FromBody] CreatePaymentLinkRequest request)
    {
        var (link, error) = await _svc.CreatePaymentLinkAsync(request, UserId, UserRole);
        if (error != null && link == null)
            return BadRequest(ApiResponse<PaymentLinkDto>.Fail(error));
        if (error != null)
            return StatusCode(502, ApiResponse<object>.Ok(new { link, error }));
        return Ok(ApiResponse<PaymentLinkDto>.Ok(link!));
    }

    [HttpPost("links/{id:int}/refresh")]
    public async Task<IActionResult> RefreshLink(int id)
    {
        var (link, error) = await _svc.RefreshStatusAsync(id, UserId, UserRole);
        if (link == null) return NotFound(ApiResponse<PaymentLinkDto>.Fail(error ?? "Payment link not found"));
        if (error != null)
            return StatusCode(502, ApiResponse<object>.Ok(new { link, error }));
        return Ok(ApiResponse<PaymentLinkDto>.Ok(link));
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var ok = await _svc.ProcessWebhookAsync(body);
        return ok ? Ok(new { received = true }) : BadRequest(new { received = false });
    }
}
