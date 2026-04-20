using Microsoft.AspNetCore.Mvc;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.DTOs.Contacts;
using SalesCRM.Core.DTOs.Schools;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/[controller]")]
public class SchoolsController : BaseApiController
{
    private readonly ISchoolService _schoolService;

    public SchoolsController(ISchoolService schoolService)
    {
        _schoolService = schoolService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSchools(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? city = null,
        [FromQuery] string? state = null,
        [FromQuery] string? board = null,
        [FromQuery] int? assignedTo = null)
    {
        var (schools, total) = await _schoolService.GetSchoolsAsync(page, limit, search, city, state, board, UserId, UserRole, assignedTo);
        return Ok(ApiResponse<object>.Ok(new { schools, total, page, limit }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSchool(int id)
    {
        var school = await _schoolService.GetSchoolByIdAsync(id);
        if (school == null) return NotFound(ApiResponse<SchoolDto>.Fail("School not found"));
        return Ok(ApiResponse<SchoolDto>.Ok(school));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSchool([FromBody] CreateSchoolRequest request)
    {
        var school = await _schoolService.CreateSchoolAsync(request);
        return Ok(ApiResponse<SchoolDto>.Ok(school));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSchool(int id, [FromBody] UpdateSchoolRequest request)
    {
        var school = await _schoolService.UpdateSchoolAsync(id, request);
        if (school == null) return NotFound(ApiResponse<SchoolDto>.Fail("School not found"));
        return Ok(ApiResponse<SchoolDto>.Ok(school));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSchool(int id)
    {
        var result = await _schoolService.DeleteSchoolAsync(id);
        if (!result) return NotFound(ApiResponse<bool>.Fail("School not found"));
        return Ok(ApiResponse<bool>.Ok(true, "School deactivated"));
    }

    [HttpGet("map")]
    public async Task<IActionResult> GetSchoolsForMap()
    {
        var schools = await _schoolService.GetSchoolsForMapAsync();
        return Ok(ApiResponse<List<SchoolGeofenceDto>>.Ok(schools));
    }

    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearbySchools(
        [FromQuery] decimal lat,
        [FromQuery] decimal lon,
        [FromQuery] decimal radiusKm = 5)
    {
        var schools = await _schoolService.GetNearbySchoolsAsync(lat, lon, radiusKm);
        return Ok(ApiResponse<List<SchoolGeofenceDto>>.Ok(schools));
    }

    // ─── Contacts ─────────────────────────────────────────────────────────────

    [HttpGet("{schoolId}/contacts")]
    public async Task<IActionResult> GetContacts(int schoolId)
    {
        var contacts = await _schoolService.GetContactsBySchoolAsync(schoolId);
        return Ok(ApiResponse<List<ContactListDto>>.Ok(contacts));
    }

    [HttpPost("{schoolId}/contacts")]
    public async Task<IActionResult> AddContact(int schoolId, [FromBody] CreateContactRequest request)
    {
        try
        {
            var contact = await _schoolService.AddContactAsync(schoolId, request);
            return Ok(ApiResponse<ContactDto>.Ok(contact));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<ContactDto>.Fail(ex.Message));
        }
    }

    [HttpPut("contacts/{contactId}")]
    public async Task<IActionResult> UpdateContact(int contactId, [FromBody] UpdateContactRequest request)
    {
        var contact = await _schoolService.UpdateContactAsync(contactId, request);
        if (contact == null) return NotFound(ApiResponse<ContactDto>.Fail("Contact not found"));
        return Ok(ApiResponse<ContactDto>.Ok(contact));
    }

    [HttpDelete("contacts/{contactId}")]
    public async Task<IActionResult> DeleteContact(int contactId)
    {
        var result = await _schoolService.DeleteContactAsync(contactId);
        if (!result) return NotFound(ApiResponse<bool>.Fail("Contact not found"));
        return Ok(ApiResponse<bool>.Ok(true, "Contact deactivated"));
    }
}
