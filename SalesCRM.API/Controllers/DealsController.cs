using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

public class DealsController : BaseApiController
{
    private readonly IDealService _dealService;

    public DealsController(IDealService dealService)
    {
        _dealService = dealService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDeals([FromQuery] PaginationParams pagination)
    {
        var result = await _dealService.GetDealsAsync(UserId, pagination);
        return Ok(ApiResponse<PaginatedResult<DealDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDeal(int id)
    {
        var deal = await _dealService.GetDealByIdAsync(id, UserId);
        if (deal == null) return NotFound(ApiResponse<object>.Fail("Deal not found"));
        return Ok(ApiResponse<DealDto>.Ok(deal));
    }

    [HttpPost]
    public async Task<IActionResult> CreateDeal([FromBody] CreateDealRequest request)
    {
        try
        {
            var deal = await _dealService.CreateDealAsync(request, UserId);
            return Ok(ApiResponse<DealDto>.Ok(deal));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/approve")]
    public async Task<IActionResult> ApproveDeal(int id, [FromBody] DealApprovalRequest request)
    {
        try
        {
            var deal = await _dealService.ApproveDealAsync(id, request, UserId);
            if (deal == null) return NotFound(ApiResponse<object>.Fail("Deal not found"));
            return Ok(ApiResponse<DealDto>.Ok(deal));
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

    [HttpGet("pending-approvals")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var deals = await _dealService.GetPendingApprovalsAsync(UserId);
        return Ok(ApiResponse<List<DealDto>>.Ok(deals));
    }
}
