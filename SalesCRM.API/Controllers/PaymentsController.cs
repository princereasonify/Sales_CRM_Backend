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
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService svc, ILogger<PaymentsController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

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

    [HttpGet("public/order-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicOrderStatus([FromQuery] string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest(ApiResponse<PublicPaymentStatusDto>.Fail("orderId is required"));

        var dto = await _svc.GetPublicStatusByOrderIdAsync(orderId);
        if (dto == null)
            return NotFound(ApiResponse<PublicPaymentStatusDto>.Fail("Order not found"));

        return Ok(ApiResponse<PublicPaymentStatusDto>.Ok(dto));
    }

    [HttpPost("public/return-log")]
    [AllowAnonymous]
    public IActionResult LogReturn([FromBody] ReturnLogRequest request)
    {
        var orderId = request?.OrderId ?? "(none)";
        var paramsJson = request?.QueryParams != null
            ? System.Text.Json.JsonSerializer.Serialize(request.QueryParams)
            : "{}";
        var url = request?.Url ?? "(none)";

        _logger.LogInformation(
            "Juspay return-url hit: orderId={OrderId} url={Url} params={Params}",
            orderId, url, paramsJson);

        return Ok(new { logged = true });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        _logger.LogInformation(
            "Juspay webhook endpoint hit: contentType={ContentType} length={Length}",
            Request.ContentType, body?.Length ?? 0);

        var ok = await _svc.ProcessWebhookAsync(body ?? string.Empty);
        return ok ? Ok(new { received = true }) : BadRequest(new { received = false });
    }
}

public class ReturnLogRequest
{
    public string? OrderId { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? QueryParams { get; set; }
}
