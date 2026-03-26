using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Subscriptions;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class SubscriptionsController : BaseApiController
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubscriptions([FromQuery] string? status)
    {
        var result = await _subscriptionService.GetSubscriptionsAsync(UserId, UserRole, status);
        return Ok(ApiResponse<List<SchoolSubscriptionDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSubscription(int id)
    {
        var result = await _subscriptionService.GetSubscriptionAsync(id);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Subscription not found"));
        return Ok(ApiResponse<SchoolSubscriptionDto>.Ok(result));
    }

    [HttpPost("{id}/provision-credentials")]
    public async Task<IActionResult> ProvisionCredentials(int id, [FromBody] ProvisionCredentialsRequest request)
    {
        if (UserRole != "SCA" && UserRole != "SH")
            return Forbid();

        var result = await _subscriptionService.ProvisionCredentialsAsync(id, request, UserId);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Subscription not found"));
        return Ok(ApiResponse<SchoolSubscriptionDto>.Ok(result, "Credentials provisioned successfully"));
    }

    [HttpPost("{id}/revoke-credentials")]
    public async Task<IActionResult> RevokeCredentials(int id)
    {
        if (UserRole != "SCA" && UserRole != "SH")
            return Forbid();

        var result = await _subscriptionService.RevokeCredentialsAsync(id);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Subscription not found"));
        return Ok(ApiResponse<SchoolSubscriptionDto>.Ok(result, "Credentials revoked"));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateSubscriptionStatusRequest request)
    {
        if (UserRole != "SCA" && UserRole != "SH")
            return Forbid();

        var result = await _subscriptionService.UpdateStatusAsync(id, request);
        if (result == null) return NotFound(ApiResponse<object>.Fail("Subscription not found"));
        return Ok(ApiResponse<SchoolSubscriptionDto>.Ok(result));
    }
}
