using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

public class LeadsController : BaseApiController
{
    private readonly ILeadService _leadService;

    public LeadsController(ILeadService leadService)
    {
        _leadService = leadService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeads(
        [FromQuery] PaginationParams pagination,
        [FromQuery] string? search,
        [FromQuery] string? stage,
        [FromQuery] string? source)
    {
        var result = await _leadService.GetLeadsAsync(UserId, pagination, search, stage, source);
        return Ok(ApiResponse<PaginatedResult<LeadListDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLead(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(id, UserId);
        if (lead == null) return NotFound(ApiResponse<object>.Fail("Lead not found"));
        return Ok(ApiResponse<LeadDto>.Ok(lead));
    }

    [HttpPost]
    public async Task<IActionResult> CreateLead([FromBody] CreateLeadRequest request)
    {
        try
        {
            var lead = await _leadService.CreateLeadAsync(request, UserId, UserRole);
            return CreatedAtAction(nameof(GetLead), new { id = lead.Id }, ApiResponse<LeadDto>.Ok(lead));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLead(int id, [FromBody] UpdateLeadRequest request)
    {
        try
        {
            var lead = await _leadService.UpdateLeadAsync(id, request, UserId);
            if (lead == null) return NotFound(ApiResponse<object>.Fail("Lead not found"));
            return Ok(ApiResponse<LeadDto>.Ok(lead));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLead(int id)
    {
        try
        {
            var deleted = await _leadService.DeleteLeadAsync(id, UserId);
            if (!deleted) return NotFound(ApiResponse<object>.Fail("Lead not found"));
            return Ok(ApiResponse<object>.Ok(null!, "Lead deleted"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("check-duplicate")]
    public async Task<IActionResult> CheckDuplicate([FromQuery] string school, [FromQuery] string city)
    {
        var exists = await _leadService.CheckDuplicateAsync(school, city);
        return Ok(ApiResponse<bool>.Ok(exists));
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipeline()
    {
        var leads = await _leadService.GetLeadsByStageAsync(UserId);
        return Ok(ApiResponse<List<LeadListDto>>.Ok(leads));
    }

    [HttpPut("{id}/assign")]
    public async Task<IActionResult> AssignLead(int id, [FromBody] AssignLeadRequest request)
    {
        try
        {
            var lead = await _leadService.AssignLeadAsync(id, request, UserId, UserRole);
            if (lead == null) return NotFound(ApiResponse<object>.Fail("Lead not found"));
            return Ok(ApiResponse<LeadDto>.Ok(lead));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("assignable-fos")]
    public async Task<IActionResult> GetAssignableFos()
    {
        var fos = await _leadService.GetAssignableFosAsync(UserId, UserRole);
        return Ok(ApiResponse<List<UserDto>>.Ok(fos));
    }
}
