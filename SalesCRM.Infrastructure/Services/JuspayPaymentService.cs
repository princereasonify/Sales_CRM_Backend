using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class JuspayPaymentService : IJuspayPaymentService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<JuspayPaymentService> _logger;

    public JuspayPaymentService(HttpClient http, IConfiguration cfg, ILogger<JuspayPaymentService> logger)
    {
        _http = http;
        _cfg = cfg;
        _logger = logger;
    }

    public async Task<JuspaySessionResult> CreateSessionAsync(
        string orderId,
        decimal amount,
        int schoolId,
        int? schoolAdminUserId,
        string? customerEmail,
        string? customerPhone,
        CancellationToken ct = default)
    {
        var apiKey = _cfg["Juspay:ApiKey"];
        var merchantId = _cfg["Juspay:MerchantId"];
        var resellerId = _cfg["Juspay:ResellerId"] ?? "hdfc_reseller";
        var pageClientId = _cfg["Juspay:PaymentPageClientId"];
        var sessionPath = _cfg["Juspay:SessionPath"] ?? "/session";
        var returnUrl = _cfg["Juspay:ReturnUrl"];

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(merchantId) ||
            string.IsNullOrWhiteSpace(pageClientId) ||
            string.IsNullOrWhiteSpace(returnUrl))
        {
            return new JuspaySessionResult(false, null, null, null,
                "{\"error\":\"juspay_not_configured\"}", 0);
        }

        var customerId = schoolAdminUserId.HasValue
            ? $"school-{schoolId}-admin-{schoolAdminUserId.Value}"
            : $"school-{schoolId}";

        var phoneDigits = string.IsNullOrWhiteSpace(customerPhone)
            ? null
            : new string(customerPhone.Where(char.IsDigit).ToArray());

        var body = new Dictionary<string, object?>
        {
            ["order_id"] = orderId,
            ["amount"] = amount.ToString("F2", CultureInfo.InvariantCulture),
            ["customer_id"] = customerId,
            ["customer_email"] = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
            ["customer_phone"] = phoneDigits,
            ["payment_page_client_id"] = pageClientId,
            ["action"] = "paymentPage",
            ["currency"] = "INR",
            ["return_url"] = returnUrl,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, sessionPath)
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", apiKey);
        req.Headers.TryAddWithoutValidation("x-merchantid", merchantId);
        req.Headers.TryAddWithoutValidation("x-customerid", customerId);
        req.Headers.TryAddWithoutValidation("x-resellerid", resellerId);

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            var status = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Juspay /session failed status={Status} body={Body}", status, Truncate(json, 1000));
                return new JuspaySessionResult(false, null, null, null, Truncate(json, 8000), status);
            }

            string? paymentUrl = null;
            string? juspayOrderRef = null;
            DateTime? expiryUtc = null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("payment_links", out var pl))
                {
                    if (pl.TryGetProperty("web", out var web) && web.ValueKind == JsonValueKind.String)
                        paymentUrl = web.GetString();
                    if (pl.TryGetProperty("expiry", out var exp) && exp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(exp.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var expDt))
                    {
                        expiryUtc = expDt;
                    }
                }
                if (paymentUrl == null && root.TryGetProperty("payment_link", out var plSingle) && plSingle.ValueKind == JsonValueKind.String)
                    paymentUrl = plSingle.GetString();
                if (paymentUrl == null && root.TryGetProperty("payment_links_web", out var plWeb) && plWeb.ValueKind == JsonValueKind.String)
                    paymentUrl = plWeb.GetString();

                if (root.TryGetProperty("juspay_order_id", out var jOrder) && jOrder.ValueKind == JsonValueKind.String)
                    juspayOrderRef = jOrder.GetString();
                else if (root.TryGetProperty("sdk_payload", out var sdk) &&
                         sdk.TryGetProperty("order_id", out var sdkOrder) && sdkOrder.ValueKind == JsonValueKind.String)
                    juspayOrderRef = sdkOrder.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Juspay /session response parse failed");
            }

            if (string.IsNullOrWhiteSpace(paymentUrl))
                return new JuspaySessionResult(false, null, juspayOrderRef, expiryUtc, Truncate(json, 8000), status);

            return new JuspaySessionResult(true, paymentUrl, juspayOrderRef, expiryUtc, Truncate(json, 8000), status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Juspay /session call threw");
            return new JuspaySessionResult(false, null, null, null,
                JsonSerializer.Serialize(new { error = ex.Message }), 0);
        }
    }

    public async Task<JuspayOrderStatusResult> GetOrderStatusAsync(string orderId, CancellationToken ct = default)
    {
        var apiKey = _cfg["Juspay:ApiKey"];
        var merchantId = _cfg["Juspay:MerchantId"];
        var resellerId = _cfg["Juspay:ResellerId"] ?? "hdfc_reseller";
        var template = _cfg["Juspay:OrderStatusPathTemplate"] ?? "/orders/{0}";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(merchantId))
        {
            return new JuspayOrderStatusResult(false, orderId, null,
                "{\"error\":\"juspay_not_configured\"}", null, 0);
        }

        var path = string.Format(CultureInfo.InvariantCulture, template, Uri.EscapeDataString(orderId));

        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", apiKey);
        req.Headers.TryAddWithoutValidation("x-merchantid", merchantId);
        req.Headers.TryAddWithoutValidation("x-customerid", orderId);
        req.Headers.TryAddWithoutValidation("x-resellerid", resellerId);

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            var status = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
                return new JuspayOrderStatusResult(false, orderId, null, Truncate(json, 8000), Truncate(json, 1000), status);

            string? statusText = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String)
                    statusText = s.GetString();
                else if (root.TryGetProperty("order", out var o) && o.TryGetProperty("status", out var os) && os.ValueKind == JsonValueKind.String)
                    statusText = os.GetString();
                else if (root.TryGetProperty("txn_detail", out var t) && t.TryGetProperty("status", out var ts) && ts.ValueKind == JsonValueKind.String)
                    statusText = ts.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Juspay /orders response parse failed");
            }

            return new JuspayOrderStatusResult(true, orderId, statusText, Truncate(json, 8000), null, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Juspay /orders call threw");
            return new JuspayOrderStatusResult(false, orderId, null,
                JsonSerializer.Serialize(new { error = ex.Message }), ex.Message, 0);
        }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
