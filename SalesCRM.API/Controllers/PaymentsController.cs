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
    private readonly IConfiguration _config;

    public PaymentsController(IPaymentService svc, ILogger<PaymentsController> logger, IConfiguration config)
    {
        _svc = svc;
        _logger = logger;
        _config = config;
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
        return Ok(ApiResponse<PaymentLinkDto>.Ok(link, error));
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

    // Juspay's SmartGateway/Payment Page redirects to the return URL via HTTP POST
    // (form-encoded). A static SPA host (nginx) returns 405 for POSTs to a page, so
    // we accept the POST here, log everything (including the full /orders response
    // from Juspay), and respond with HTTP 303 See Other. 303 forces the browser to
    // change the method to GET when following the redirect, which is what the SPA
    // host can serve. Plain 302 leaves the method ambiguous and some browsers
    // preserve POST → nginx 405.
    [HttpGet("gateway-return")]
    [HttpPost("gateway-return")]
    [AllowAnonymous]
    public async Task<IActionResult> GatewayReturn()
    {
        var dict = new Dictionary<string, string>();

        foreach (var kv in Request.Query)
        {
            var v = kv.Value.ToString();
            if (!string.IsNullOrEmpty(v)) dict[kv.Key] = v;
        }

        if (HttpMethods.IsPost(Request.Method) && Request.HasFormContentType)
        {
            try
            {
                var form = await Request.ReadFormAsync();
                foreach (var kv in form)
                {
                    var v = kv.Value.ToString();
                    if (!string.IsNullOrEmpty(v)) dict[kv.Key] = v;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Payment gateway return: failed to read form body");
            }
        }

        dict.TryGetValue("order_id", out var orderId);
        if (string.IsNullOrEmpty(orderId)) dict.TryGetValue("orderId", out orderId);
        dict.TryGetValue("status", out var status);

        _logger.LogInformation(
            "Payment gateway return hit: method={Method} orderId={OrderId} status={Status} params={Params}",
            Request.Method, orderId ?? "(none)", status ?? "(none)",
            System.Text.Json.JsonSerializer.Serialize(dict));

        // Sync the authoritative status from Juspay /orders so the row reflects the
        // final state, and the full gateway response gets logged before we redirect.
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            try
            {
                await _svc.GetPublicStatusByOrderIdAsync(orderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Payment gateway return: failed to sync /orders for orderId={OrderId}", orderId);
            }
        }

        var frontend = _config["Juspay:FrontendReturnUrl"];
        if (string.IsNullOrWhiteSpace(frontend))
        {
            _logger.LogError("Juspay:FrontendReturnUrl is not configured — cannot redirect");
            return StatusCode(500, ApiResponse<object>.Fail("Juspay:FrontendReturnUrl is not configured"));
        }

        var qs = string.Join("&", dict.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var separator = frontend.Contains('?') ? "&" : "?";
        var redirectUrl = qs.Length > 0 ? $"{frontend}{separator}{qs}" : frontend;

        // 303 See Other forces the browser to issue a GET to the SPA, even if the
        // gateway hit us with POST. This is the fix for the nginx "405 Not Allowed"
        // on /payment/response — static hosts don't accept POST.
        Response.Headers.Location = redirectUrl;
        return StatusCode(StatusCodes.Status303SeeOther);
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
