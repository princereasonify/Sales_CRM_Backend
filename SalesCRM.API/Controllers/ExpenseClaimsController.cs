using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Expense;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/expense-claims")]
public class ExpenseClaimsController : BaseApiController
{
    private readonly IExpenseClaimService _svc;
    private readonly IGcpStorageService _storage;

    public ExpenseClaimsController(IExpenseClaimService svc, IGcpStorageService storage)
    {
        _svc = svc;
        _storage = storage;
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> CreateClaim(
        [FromForm] CreateExpenseClaimRequest request,
        IFormFile? bill,
        CancellationToken ct)
    {
        string? billUrl = null;

        if (bill != null && bill.Length > 0)
        {
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
            if (!allowedTypes.Contains(bill.ContentType.ToLower()))
                return BadRequest(ApiResponse<object>.Fail("Only JPEG, PNG, WebP, and PDF files are allowed."));

            if (bill.Length > 10 * 1024 * 1024)
                return BadRequest(ApiResponse<object>.Fail("File size must be under 10MB."));

            var ext = Path.GetExtension(bill.FileName).ToLowerInvariant();
            var objectName = $"SalesCRMAllowances/{Guid.NewGuid():N}{ext}";

            await using var stream = bill.OpenReadStream();
            var result = await _storage.UploadFileAsync(objectName, stream, bill.ContentType, ct);

            if (!result.Success)
                return StatusCode(500, ApiResponse<object>.Fail(result.Error ?? "Upload failed."));

            billUrl = result.PublicUrl;
        }

        try
        {
            var claim = await _svc.CreateClaimAsync(request, billUrl, UserId);
            return Ok(ApiResponse<ExpenseClaimDto>.Ok(claim, "Expense claim submitted"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyClaims(
        [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var claims = await _svc.GetMyClaimsAsync(UserId, status, category, from, to);
        return Ok(ApiResponse<List<ExpenseClaimDto>>.Ok(claims));
    }

    [HttpGet("team")]
    public async Task<IActionResult> GetTeamClaims(
        [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (UserRole == "FO") return Forbid();
        var claims = await _svc.GetTeamClaimsAsync(UserId, UserRole, status, category, from, to);
        return Ok(ApiResponse<List<ExpenseClaimDto>>.Ok(claims));
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveClaim(int id)
    {
        if (UserRole == "FO") return Forbid();
        try
        {
            var claim = await _svc.ApproveClaimAsync(id, UserId);
            if (claim == null) return NotFound(ApiResponse<object>.Fail("Claim not found or not pending"));
            return Ok(ApiResponse<ExpenseClaimDto>.Ok(claim, "Expense claim approved"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectClaim(int id, [FromBody] RejectExpenseClaimRequest request)
    {
        if (UserRole == "FO") return Forbid();
        try
        {
            var claim = await _svc.RejectClaimAsync(id, request, UserId);
            if (claim == null) return NotFound(ApiResponse<object>.Fail("Claim not found or not pending"));
            return Ok(ApiResponse<ExpenseClaimDto>.Ok(claim, "Expense claim rejected"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("bulk-approve")]
    public async Task<IActionResult> BulkApprove([FromBody] BulkApproveExpenseRequest request)
    {
        if (UserRole == "FO") return Forbid();
        var count = await _svc.BulkApproveClaimsAsync(request.Ids, UserId);
        return Ok(ApiResponse<object>.Ok(new { count }, $"{count} expense claims approved"));
    }
}
