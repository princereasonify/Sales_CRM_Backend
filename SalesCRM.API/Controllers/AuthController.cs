using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Auth;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUnitOfWork _unitOfWork;

    public AuthController(IAuthService authService, IUnitOfWork unitOfWork)
    {
        _authService = authService;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(ApiResponse<object>.Fail("Invalid email or password"));

        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(ApiResponse<object>.Ok(null, "Logged out successfully"));
    }

    [Authorize]
    [HttpPost("create-user")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        if (creatorRole == "FO")
            return Forbid();

        try
        {
            var user = await _authService.CreateUserAsync(request, creatorRole);
            return Ok(ApiResponse<Core.DTOs.UserDto>.Ok(user, "User created successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var creatorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        if (creatorRole == "FO")
            return Forbid();

        var users = await _authService.GetUsersCreatedByRoleAsync(creatorRole, creatorId);
        return Ok(ApiResponse<List<Core.DTOs.UserDto>>.Ok(users));
    }

    [Authorize]
    [HttpPut("update-user/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        if (creatorRole == "FO")
            return Forbid();

        try
        {
            var user = await _authService.UpdateUserAsync(id, request, creatorRole);
            return Ok(ApiResponse<Core.DTOs.UserDto>.Ok(user, "User updated successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpDelete("delete-user/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        if (creatorRole == "FO")
            return Forbid();

        try
        {
            await _authService.DeleteUserAsync(id, creatorRole);
            return Ok(ApiResponse<object>.Ok(null, "User deleted successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("zones")]
    public async Task<IActionResult> GetZones()
    {
        var zones = await _unitOfWork.Zones.Query()
            .Include(z => z.Region)
            .OrderBy(z => z.Name)
            .Select(z => new { z.Id, z.Name, z.RegionId, Region = z.Region != null ? z.Region.Name : null })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(zones));
    }

    [Authorize]
    [HttpGet("regions")]
    public async Task<IActionResult> GetRegions()
    {
        var regions = await _unitOfWork.Regions.Query()
            .Include(r => r.Zones)
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, ZoneCount = r.Zones.Count })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(regions));
    }

    // ── Region CRUD ──

    [Authorize]
    [HttpPost("regions")]
    public async Task<IActionResult> CreateRegion([FromBody] NameRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH")
            return Forbid();

        var name = request.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
            return BadRequest(ApiResponse<object>.Fail("Region name is required"));

        var exists = await _unitOfWork.Regions.Query()
            .AnyAsync(r => r.Name.ToLower() == name.ToLower());
        if (exists)
            return BadRequest(ApiResponse<object>.Fail($"Region '{name}' already exists"));

        var region = new SalesCRM.Core.Entities.Region { Name = name };
        await _unitOfWork.Regions.AddAsync(region);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { region.Id, region.Name, ZoneCount = 0 }, "Region created"));
    }

    [Authorize]
    [HttpPut("regions/{id}")]
    public async Task<IActionResult> UpdateRegion(int id, [FromBody] NameRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH")
            return Forbid();

        var region = await _unitOfWork.Regions.GetByIdAsync(id);
        if (region == null)
            return NotFound(ApiResponse<object>.Fail("Region not found"));

        var name = request.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
            return BadRequest(ApiResponse<object>.Fail("Region name is required"));

        var exists = await _unitOfWork.Regions.Query()
            .AnyAsync(r => r.Name.ToLower() == name.ToLower() && r.Id != id);
        if (exists)
            return BadRequest(ApiResponse<object>.Fail($"Region '{name}' already exists"));

        region.Name = name;
        await _unitOfWork.Regions.UpdateAsync(region);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { region.Id, region.Name }, "Region updated"));
    }

    [Authorize]
    [HttpDelete("regions/{id}")]
    public async Task<IActionResult> DeleteRegion(int id)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH")
            return Forbid();

        var region = await _unitOfWork.Regions.Query()
            .Include(r => r.Zones)
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (region == null)
            return NotFound(ApiResponse<object>.Fail("Region not found"));

        if (region.Zones.Any())
            return BadRequest(ApiResponse<object>.Fail("Cannot delete region with zones. Delete zones first."));

        if (region.Users.Any())
            return BadRequest(ApiResponse<object>.Fail("Cannot delete region with assigned users."));

        await _unitOfWork.Regions.DeleteAsync(region);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Region deleted"));
    }

    // ── Zone CRUD ──

    [Authorize]
    [HttpPost("zones")]
    public async Task<IActionResult> CreateZone([FromBody] CreateZoneRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH")
            return Forbid();

        var name = request.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
            return BadRequest(ApiResponse<object>.Fail("Zone name is required"));

        if (request.RegionId <= 0)
            return BadRequest(ApiResponse<object>.Fail("Region is required"));

        var region = await _unitOfWork.Regions.GetByIdAsync(request.RegionId);
        if (region == null)
            return BadRequest(ApiResponse<object>.Fail("Region not found"));

        var exists = await _unitOfWork.Zones.Query()
            .AnyAsync(z => z.Name.ToLower() == name.ToLower());
        if (exists)
            return BadRequest(ApiResponse<object>.Fail($"Zone '{name}' already exists"));

        var zone = new SalesCRM.Core.Entities.Zone { Name = name, RegionId = request.RegionId };
        await _unitOfWork.Zones.AddAsync(zone);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { zone.Id, zone.Name, zone.RegionId, Region = region.Name }, "Zone created"));
    }

    [Authorize]
    [HttpPut("zones/{id}")]
    public async Task<IActionResult> UpdateZone(int id, [FromBody] CreateZoneRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH")
            return Forbid();

        var zone = await _unitOfWork.Zones.Query()
            .Include(z => z.Region)
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
            return NotFound(ApiResponse<object>.Fail("Zone not found"));

        var name = request.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
            return BadRequest(ApiResponse<object>.Fail("Zone name is required"));

        if (request.RegionId <= 0)
            return BadRequest(ApiResponse<object>.Fail("Region is required"));

        var exists = await _unitOfWork.Zones.Query()
            .AnyAsync(z => z.Name.ToLower() == name.ToLower() && z.Id != id);
        if (exists)
            return BadRequest(ApiResponse<object>.Fail($"Zone '{name}' already exists"));

        zone.Name = name;
        zone.RegionId = request.RegionId;
        await _unitOfWork.Zones.UpdateAsync(zone);
        await _unitOfWork.SaveChangesAsync();

        var region = await _unitOfWork.Regions.GetByIdAsync(request.RegionId);

        return Ok(ApiResponse<object>.Ok(new { zone.Id, zone.Name, zone.RegionId, Region = region?.Name }, "Zone updated"));
    }

    [Authorize]
    [HttpDelete("zones/{id}")]
    public async Task<IActionResult> DeleteZone(int id)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH")
            return Forbid();

        var zone = await _unitOfWork.Zones.Query()
            .Include(z => z.Users)
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
            return NotFound(ApiResponse<object>.Fail("Zone not found"));

        if (zone.Users.Any())
            return BadRequest(ApiResponse<object>.Fail("Cannot delete zone with assigned users."));

        await _unitOfWork.Zones.DeleteAsync(zone);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Zone deleted"));
    }
}
