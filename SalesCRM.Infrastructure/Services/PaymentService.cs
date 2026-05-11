using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.DTOs.Payments;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private static readonly string[] PaidStatuses = {
        "CHARGED", "AUTHORIZATION_SUCCEEDED", "CAPTURED",
        "AUTHENTICATION_SUCCESSFUL", "AUTHORIZED", "SUCCESS",
    };
    private static readonly string[] FailedStatuses = {
        "FAILED", "AUTHENTICATION_FAILED", "AUTHORIZATION_FAILED",
        "JUSPAY_DECLINED", "DECLINED", "VOIDED", "EXPIRED", "ERROR",
        "USER_ABORTED", "TERMINUS_FAILED",
    };
    private const string OrderIdAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private readonly IUnitOfWork _uow;
    private readonly IJuspayPaymentService _juspay;
    private readonly INotificationService _notify;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IUnitOfWork uow, IJuspayPaymentService juspay, INotificationService notify,
        ILogger<PaymentService> logger)
    {
        _uow = uow;
        _juspay = juspay;
        _notify = notify;
        _logger = logger;
    }

    public async Task<List<EligibleSchoolDto>> GetEligibleSchoolsAsync(int userId, string userRole)
    {
        var caller = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == userId);
        if (caller == null) return new();

        // Schools that have at least one Lead won by an FO in scope, AND have an OnboardAssignment.
        var leadsQ = _uow.Leads.Query().Include(l => l.Fo).AsQueryable();
        leadsQ = caller.Role switch
        {
            UserRole.FO => leadsQ.Where(l => l.FoId == userId),
            UserRole.ZH => leadsQ.Where(l => l.Fo.ZoneId == caller.ZoneId),
            UserRole.RH => leadsQ.Where(l => l.Fo.RegionId == caller.RegionId),
            UserRole.SH or UserRole.SCA => leadsQ,
            _ => leadsQ.Where(l => false)
        };

        var wonLeads = await leadsQ
            .Where(l => l.Stage == LeadStage.Won || l.Stage == LeadStage.ImplementationStarted)
            .Select(l => new
            {
                l.School,
                l.City,
                l.ContactName,
                l.ContactEmail,
                l.ContactPhone,
            })
            .ToListAsync();

        if (wonLeads.Count == 0) return new();

        // Match leads to School master rows by name+city.
        var nameCityKeys = wonLeads
            .Select(l => new { Name = (l.School ?? "").Trim(), City = (l.City ?? "").Trim() })
            .Where(k => k.Name.Length > 0)
            .Distinct()
            .ToList();

        var allSchools = await _uow.Schools.Query()
            .Where(s => s.IsActive)
            .Select(s => new { s.Id, s.Name, s.City })
            .ToListAsync();

        var schoolIdByKey = allSchools
            .GroupBy(s => new { Name = (s.Name ?? "").Trim(), City = (s.City ?? "").Trim() })
            .ToDictionary(g => g.Key, g => g.First().Id);

        var matchedSchoolIds = nameCityKeys
            .Select(k => schoolIdByKey.TryGetValue(new { k.Name, k.City }, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToHashSet();

        if (matchedSchoolIds.Count == 0) return new();

        // Exclude schools that already have at least one successfully paid PaymentLink —
        // once payment is collected, the school disappears from the "create link" dropdown.
        var paidSchoolIds = await _uow.PaymentLinks.Query()
            .Where(p => !p.IsDeleted && p.Status == "paid" && matchedSchoolIds.Contains(p.SchoolId))
            .Select(p => p.SchoolId)
            .Distinct()
            .ToListAsync();
        var paidSet = paidSchoolIds.ToHashSet();
        matchedSchoolIds.ExceptWith(paidSet);

        if (matchedSchoolIds.Count == 0) return new();

        // Build the result keyed by school id, picking the most recent contact info for each.
        var leadByKey = wonLeads
            .GroupBy(l => new { Name = (l.School ?? "").Trim(), City = (l.City ?? "").Trim() })
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<EligibleSchoolDto>();
        foreach (var s in allSchools.Where(s => matchedSchoolIds.Contains(s.Id)))
        {
            var key = new { Name = (s.Name ?? "").Trim(), City = (s.City ?? "").Trim() };
            leadByKey.TryGetValue(key, out var lead);
            result.Add(new EligibleSchoolDto
            {
                SchoolId = s.Id,
                SchoolName = s.Name ?? "",
                City = s.City,
                ContactName = lead?.ContactName,
                ContactEmail = lead?.ContactEmail,
                ContactPhone = lead?.ContactPhone,
            });
        }

        return result.OrderBy(r => r.SchoolName).ToList();
    }

    public async Task<(List<PaymentLinkDto> Items, int Total)> GetPaymentLinksAsync(
        int userId, string userRole, int? schoolId, string? status, int page, int limit)
    {
        var caller = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == userId);
        if (caller == null) return (new(), 0);

        var q = _uow.PaymentLinks.Query()
            .Include(p => p.School)
            .Include(p => p.CreatedBy)
            .Where(p => !p.IsDeleted);

        q = await ApplyScopeAsync(q, caller);

        if (schoolId.HasValue) q = q.Where(p => p.SchoolId == schoolId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(p => p.Status == status.ToLower());

        var total = await q.CountAsync();
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;
        if (limit > 100) limit = 100;

        var items = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * limit).Take(limit).ToListAsync();

        return (items.Select(ToDto).ToList(), total);
    }

    public async Task<PaymentLinkDto?> GetPaymentLinkByIdAsync(int id, int userId, string userRole)
    {
        var caller = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == userId);
        if (caller == null) return null;

        var row = await _uow.PaymentLinks.Query()
            .Include(p => p.School)
            .Include(p => p.CreatedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (row == null) return null;
        if (!await IsAccessibleAsync(row, caller)) return null;
        return ToDto(row);
    }

    public async Task<(PaymentLinkDto? Link, string? Error)> CreatePaymentLinkAsync(
        CreatePaymentLinkRequest request, int userId, string userRole)
    {
        if (request.SchoolId <= 0) return (null, "schoolId is required");
        if (request.Amount <= 0) return (null, "amount must be greater than zero");

        var caller = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == userId);
        if (caller == null) return (null, "User not found");

        var eligible = await GetEligibleSchoolsAsync(userId, userRole);
        var schoolEntry = eligible.FirstOrDefault(s => s.SchoolId == request.SchoolId);
        if (schoolEntry == null)
            return (null, "School is not eligible (deal must be won and onboarded).");

        var school = await _uow.Schools.GetByIdAsync(request.SchoolId);
        if (school == null) return (null, "School not found");

        var orderId = NewMerchantOrderId(school.Id);
        var dueDateUtc = DateTime.SpecifyKind(request.DueDate.Date, DateTimeKind.Utc);

        var link = new PaymentLink
        {
            SchoolId = school.Id,
            OrderId = orderId,
            Amount = request.Amount,
            Currency = "INR",
            Description = request.Description,
            DueDate = dueDateUtc,
            Status = "pending",
            CreatedById = userId,
        };

        await _uow.PaymentLinks.AddAsync(link);
        await _uow.SaveChangesAsync();

        // Look up a school-portal admin email/phone if a SchoolProfile exists; otherwise fall back to the lead contact.
        var profile = await _uow.SchoolProfiles.Query()
            .Where(p => p.SchoolId == school.Id)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        var customerEmail = profile?.UserEmail;
        if (string.IsNullOrWhiteSpace(customerEmail)) customerEmail = profile?.SchoolEmail;
        if (string.IsNullOrWhiteSpace(customerEmail)) customerEmail = schoolEntry.ContactEmail;
        if (string.IsNullOrWhiteSpace(customerEmail)) customerEmail = school.Email;

        var customerPhone = profile?.UserPhone;
        if (string.IsNullOrWhiteSpace(customerPhone)) customerPhone = profile?.SchoolPhone;
        if (string.IsNullOrWhiteSpace(customerPhone)) customerPhone = schoolEntry.ContactPhone;
        if (string.IsNullOrWhiteSpace(customerPhone)) customerPhone = school.Phone;

        var session = await _juspay.CreateSessionAsync(
            orderId, request.Amount, school.Id, profile?.CreatedById,
            customerEmail, customerPhone);

        if (!session.Success || string.IsNullOrWhiteSpace(session.PaymentUrl))
        {
            link.Status = "failed";
            link.LastWebhookPayload = JsonSerializer.Serialize(new
            {
                error = "session_create_failed",
                http = session.HttpStatus,
                body = Truncate(session.RawJson, 8000),
            });
            link.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync();
            return (ToDto(await ReloadAsync(link.Id)), "Payment gateway error. Please try again later.");
        }

        link.PaymentUrl = session.PaymentUrl;
        link.JuspayOrderRef = session.JuspayOrderRef;
        if (session.ExpiryAtUtc.HasValue)
            link.ExpiryAt = DateTime.SpecifyKind(session.ExpiryAtUtc.Value, DateTimeKind.Utc);
        link.Status = "pending";
        link.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync();

        try
        {
            await _notify.CreateNotificationAsync(
                userId,
                NotificationType.Success,
                $"Payment link created: {school.Name}",
                $"Link for ₹{request.Amount:N0} created. Share with the school admin.");
        }
        catch { }

        return (ToDto(await ReloadAsync(link.Id)), null);
    }

    public async Task<(PaymentLinkDto? Link, string? Error)> RefreshStatusAsync(int id, int userId, string userRole)
    {
        var caller = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == userId);
        if (caller == null) return (null, "User not found");

        var row = await _uow.PaymentLinks.Query()
            .Include(p => p.School)
            .Include(p => p.CreatedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (row == null) return (null, "Payment link not found");
        if (!await IsAccessibleAsync(row, caller)) return (null, "Access denied");

        try
        {
            var status = await _juspay.GetOrderStatusAsync(row.OrderId);
            if (!status.Success)
                return (ToDto(row), "Gateway error. Status unchanged.");

            await ApplyOrderStatusAsync(row, status.Status);
            return (ToDto(await ReloadAsync(row.Id)), null);
        }
        catch (Exception ex)
        {
            return (ToDto(row), $"Gateway error: {ex.Message}");
        }
    }

    public async Task<PublicPaymentStatusDto?> GetPublicStatusByOrderIdAsync(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return null;

        var row = await _uow.PaymentLinks.Query()
            .Include(p => p.School)
            .FirstOrDefaultAsync(p => p.OrderId == orderId && !p.IsDeleted);

        if (row == null) return null;

        // Real-time: if the row is not yet in a terminal state, ask Juspay for the latest
        // status and apply it before returning. Swallow gateway errors so the page still loads.
        if (row.Status != "paid" && row.Status != "failed")
        {
            try
            {
                var status = await _juspay.GetOrderStatusAsync(row.OrderId);
                if (status.Success)
                    await ApplyOrderStatusAsync(row, status.Status);
            }
            catch { }
        }

        return new PublicPaymentStatusDto
        {
            OrderId = row.OrderId,
            Amount = row.Amount,
            Currency = row.Currency,
            Status = row.Status,
            PaidAt = row.PaidAt,
            SchoolName = row.School?.Name ?? string.Empty,
        };
    }

    public async Task<bool> ProcessWebhookAsync(string rawBody)
    {
        _logger.LogInformation("Juspay webhook received: length={Length} preview={Preview}",
            rawBody?.Length ?? 0, Truncate(rawBody ?? "", 800));

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning("Juspay webhook: empty body");
            return false;
        }

        string? orderId = null;
        string? payloadStatus = null;
        string? eventName = null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            orderId = TryReadString(root, "order_id")
                   ?? TryReadString(root, "orderId")
                   ?? (root.TryGetProperty("content", out var content) ? TryReadString(content, "order_id") : null)
                   ?? (root.TryGetProperty("payload", out var payload) ? TryReadString(payload, "order_id") : null);

            payloadStatus = ExtractStatusFromWebhook(root);
            eventName = TryReadString(root, "event_name");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Juspay webhook: body is not valid JSON");
            return false;
        }

        _logger.LogInformation("Juspay webhook parsed: event={Event} orderId={OrderId} payloadStatus={Status}",
            eventName, orderId, payloadStatus);

        if (string.IsNullOrWhiteSpace(orderId))
        {
            _logger.LogWarning("Juspay webhook: no order_id in body");
            return false;
        }

        var row = await _uow.PaymentLinks.Query()
            .Include(p => p.School)
            .Include(p => p.CreatedBy)
            .FirstOrDefaultAsync(p => p.OrderId == orderId && !p.IsDeleted);

        if (row == null)
        {
            _logger.LogWarning("Juspay webhook: no payment link found for orderId={OrderId} (acking anyway)", orderId);
            return true; // ack so Juspay stops retrying for unknown orders
        }

        row.LastWebhookPayload = Truncate(rawBody, 12000);
        row.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync();

        // Prefer Juspay /orders API as source of truth, but fall back to the status carried
        // in the webhook payload itself so the row still transitions when /orders is unavailable.
        string? finalStatus = null;
        try
        {
            var statusRes = await _juspay.GetOrderStatusAsync(row.OrderId);
            if (statusRes.Success && !string.IsNullOrWhiteSpace(statusRes.Status))
            {
                finalStatus = statusRes.Status;
                _logger.LogInformation("Juspay /orders status: orderId={OrderId} status={Status}", row.OrderId, finalStatus);
            }
            else
            {
                _logger.LogWarning("Juspay /orders call did not return a usable status: orderId={OrderId} http={Http}",
                    row.OrderId, statusRes.HttpStatus);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Juspay /orders call threw for orderId={OrderId}", row.OrderId);
        }

        if (string.IsNullOrWhiteSpace(finalStatus))
            finalStatus = payloadStatus;

        if (string.IsNullOrWhiteSpace(finalStatus))
        {
            _logger.LogWarning("Juspay webhook: no usable status from /orders or payload, row left unchanged. orderId={OrderId}",
                row.OrderId);
            return false;
        }

        await ApplyOrderStatusAsync(row, finalStatus);
        _logger.LogInformation("Juspay webhook applied: orderId={OrderId} appliedStatus={Status} rowStatus={RowStatus}",
            row.OrderId, finalStatus, row.Status);
        return true;
    }

    private static string? ExtractStatusFromWebhook(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;

        // Direct status field on the root.
        var direct = TryReadString(root, "status");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        // content.{order|txn_detail|txn|payment}.status
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in new[] { "order", "txn_detail", "txn", "payment" })
            {
                if (content.TryGetProperty(prop, out var inner) && inner.ValueKind == JsonValueKind.Object)
                {
                    var s = TryReadString(inner, "status");
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            var contentStatus = TryReadString(content, "status");
            if (!string.IsNullOrWhiteSpace(contentStatus)) return contentStatus;
        }

        // payload.{status|order.status}
        if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
        {
            var ps = TryReadString(payload, "status");
            if (!string.IsNullOrWhiteSpace(ps)) return ps;
            if (payload.TryGetProperty("order", out var pOrder) && pOrder.ValueKind == JsonValueKind.Object)
            {
                var pos = TryReadString(pOrder, "status");
                if (!string.IsNullOrWhiteSpace(pos)) return pos;
            }
        }

        // Last resort: derive a synthetic status from event_name (e.g. ORDER_SUCCEEDED, ORDER_FAILED).
        var eventName = TryReadString(root, "event_name");
        if (!string.IsNullOrWhiteSpace(eventName))
        {
            var en = eventName.ToUpperInvariant();
            if (en.Contains("SUCCEEDED") || en.Contains("CHARGED") || en.Contains("CAPTURED"))
                return "CHARGED";
            if (en.Contains("FAILED") || en.Contains("DECLINED") || en.Contains("EXPIRED") || en.Contains("ABORTED"))
                return "FAILED";
        }

        return null;
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private async Task ApplyOrderStatusAsync(PaymentLink row, string? statusText)
    {
        var s = (statusText ?? "").Trim().ToUpperInvariant();
        var transitioned = false;

        if (PaidStatuses.Contains(s))
        {
            if (row.Status != "paid")
            {
                row.Status = "paid";
                row.PaidAt = DateTime.UtcNow;
                transitioned = true;
            }
        }
        else if (FailedStatuses.Contains(s))
        {
            if (row.Status != "paid" && row.Status != "failed")
            {
                row.Status = "failed";
                transitioned = true;
            }
        }
        // For any other Juspay status (NEW / PENDING_VBV / AUTHORIZING / etc.) we
        // leave the row at its current state — link is alive and waiting for payment.

        row.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync();

        if (transitioned && row.CreatedById > 0)
        {
            try
            {
                if (row.Status == "paid")
                    await _notify.CreateNotificationAsync(row.CreatedById, NotificationType.Success,
                        $"Payment received: {row.School?.Name ?? "School"}",
                        $"₹{row.Amount:N0} for order {row.OrderId} has been paid.");
                else if (row.Status == "failed")
                    await _notify.CreateNotificationAsync(row.CreatedById, NotificationType.Warning,
                        $"Payment failed: {row.School?.Name ?? "School"}",
                        $"Payment of ₹{row.Amount:N0} for order {row.OrderId} failed.");
            }
            catch { }
        }
    }

    private async Task<IQueryable<PaymentLink>> ApplyScopeAsync(IQueryable<PaymentLink> q, User caller)
    {
        switch (caller.Role)
        {
            case UserRole.SH:
            case UserRole.SCA:
                return q;
            case UserRole.FO:
                return q.Where(p => p.CreatedById == caller.Id);
            case UserRole.ZH:
                {
                    var zoneFoIds = await _uow.Users.Query()
                        .Where(u => u.Role == UserRole.FO && u.ZoneId == caller.ZoneId)
                        .Select(u => u.Id).ToListAsync();
                    var ids = zoneFoIds.Append(caller.Id).Distinct().ToList();
                    return q.Where(p => ids.Contains(p.CreatedById));
                }
            case UserRole.RH:
                {
                    var regionFoIds = await _uow.Users.Query()
                        .Where(u => u.Role == UserRole.FO && u.RegionId == caller.RegionId)
                        .Select(u => u.Id).ToListAsync();
                    var ids = regionFoIds.Append(caller.Id).Distinct().ToList();
                    return q.Where(p => ids.Contains(p.CreatedById));
                }
            default:
                return q.Where(p => p.CreatedById == caller.Id);
        }
    }

    private async Task<bool> IsAccessibleAsync(PaymentLink row, User caller)
    {
        if (caller.Role == UserRole.SH || caller.Role == UserRole.SCA) return true;
        if (row.CreatedById == caller.Id) return true;

        var creator = await _uow.Users.Query().FirstOrDefaultAsync(u => u.Id == row.CreatedById);
        if (creator == null) return false;

        return caller.Role switch
        {
            UserRole.ZH => creator.ZoneId == caller.ZoneId,
            UserRole.RH => creator.RegionId == caller.RegionId,
            _ => false
        };
    }

    private async Task<PaymentLink> ReloadAsync(int id)
    {
        return await _uow.PaymentLinks.Query()
            .Include(p => p.School)
            .Include(p => p.CreatedBy)
            .FirstAsync(p => p.Id == id);
    }

    private static PaymentLinkDto ToDto(PaymentLink p) => new()
    {
        Id = p.Id,
        SchoolId = p.SchoolId,
        SchoolName = p.School?.Name ?? "",
        OrderId = p.OrderId,
        JuspayOrderRef = p.JuspayOrderRef,
        Amount = p.Amount,
        Currency = p.Currency,
        Description = p.Description,
        DueDate = p.DueDate,
        PaymentUrl = p.PaymentUrl,
        ExpiryAt = p.ExpiryAt,
        Status = p.Status,
        PaidAt = p.PaidAt,
        CreatedById = p.CreatedById,
        CreatedByName = p.CreatedBy?.Name ?? "",
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };

    private static string NewMerchantOrderId(int schoolId)
    {
        var prefix = "P" + (schoolId % 10000).ToString("D4", CultureInfo.InvariantCulture);
        var timestamp = DateTime.UtcNow.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture);
        var rng = RandomNumberGenerator.GetBytes(4);
        var suffix = new char[4];
        for (int i = 0; i < 4; i++) suffix[i] = OrderIdAlphabet[rng[i] % OrderIdAlphabet.Length];
        return prefix + timestamp + new string(suffix);
    }

    private static string? TryReadString(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
