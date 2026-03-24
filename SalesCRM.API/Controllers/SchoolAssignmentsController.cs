using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.SchoolAssignment;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/school-assignments")]
public class SchoolAssignmentsController : BaseApiController
{
    private readonly ISchoolAssignmentService _service;

    public SchoolAssignmentsController(ISchoolAssignmentService service)
    {
        _service = service;
    }

    /// <summary>Bulk assign schools to an FO for a date</summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkAssign([FromBody] BulkAssignRequest request)
    {
        var result = await _service.BulkAssignAsync(UserId, request);
        return Ok(ApiResponse<List<SchoolAssignmentDto>>.Ok(result));
    }

    /// <summary>Get assignments for a specific FO on a date</summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(int userId, [FromQuery] string date)
    {
        var result = await _service.GetAssignmentsAsync(userId, date);
        return Ok(ApiResponse<List<SchoolAssignmentDto>>.Ok(result));
    }

    /// <summary>Get my assignments for today (used by FO)</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyAssignments([FromQuery] string date)
    {
        var result = await _service.GetAssignmentsAsync(UserId, date);
        return Ok(ApiResponse<List<SchoolAssignmentDto>>.Ok(result));
    }

    /// <summary>Get all assignments in manager's scope for a date</summary>
    [HttpGet("team")]
    public async Task<IActionResult> GetTeamAssignments([FromQuery] string date)
    {
        var result = await _service.GetAssignmentsByManagerAsync(UserId, UserRole, date);
        return Ok(ApiResponse<List<SchoolAssignmentDto>>.Ok(result));
    }

    /// <summary>Delete an assignment</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAssignmentAsync(id, UserId);
        if (!success) return NotFound(ApiResponse<object>.Fail("Assignment not found"));
        return Ok(ApiResponse<object>.Ok(null));
    }
}
