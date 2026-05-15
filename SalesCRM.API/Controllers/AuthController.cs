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
    private readonly IDeviceFraudService _deviceFraudService;

    public AuthController(IAuthService authService, IUnitOfWork unitOfWork, IDeviceFraudService deviceFraudService)
    {
        _authService = authService;
        _unitOfWork = unitOfWork;
        _deviceFraudService = deviceFraudService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        LoginResponse? result;
        try
        {
            result = await _authService.LoginAsync(request);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(ApiResponse<object>.Fail(ex.Message));
        }

        if (result == null)
            return Unauthorized(ApiResponse<object>.Fail("Invalid email or password"));

        // Capture device info for fraud detection (fire-and-forget, don't block login)
        var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var fraudService = scope.ServiceProvider.GetRequiredService<IDeviceFraudService>();
                await fraudService.ProcessLoginAsync(result.User.Id, request.DeviceInfo, ipAddress, userAgent);
            }
            catch { /* Don't let fraud detection failure affect login */ }
        });

        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    [HttpPost("delete-account-request")]
    public async Task<IActionResult> DeleteAccountRequest([FromBody] LoginRequest request)
    {
        try
        {
            await _authService.RequestAccountDeletionAsync(request.Email, request.Password);
            return Ok(ApiResponse<object>.Ok(null, "Account deletion request submitted. You will receive a confirmation email shortly."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (System.Net.Mail.SmtpException)
        {
            return StatusCode(500, ApiResponse<object>.Fail("Failed to send confirmation email. Please try again later."));
        }
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request)
    {
        try
        {
            var user = await _authService.SignupAsync(request);
            return Ok(ApiResponse<Core.DTOs.UserDto>.Ok(user, "Signed up successfully! Your account is pending admin approval. You will be able to login once approved."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("approve-user/{id}")]
    public async Task<IActionResult> ApproveUser(int id)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SCA")
            return Forbid();

        try
        {
            var user = await _authService.ApproveUserAsync(id);
            return Ok(ApiResponse<Core.DTOs.UserDto>.Ok(user, "User approved successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("reject-user/{id}")]
    public async Task<IActionResult> RejectUser(int id)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SCA")
            return Forbid();

        try
        {
            await _authService.RejectUserAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "User rejected and removed successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("pending-users")]
    public async Task<IActionResult> GetPendingUsers()
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SCA")
            return Forbid();

        var users = await _authService.GetPendingUsersAsync();
        return Ok(ApiResponse<List<Core.DTOs.UserDto>>.Ok(users));
    }

    [Authorize]
    [HttpPost("home-location")]
    public async Task<IActionResult> SetHomeLocation([FromBody] SetHomeLocationRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        try
        {
            var user = await _authService.SetHomeLocationAsync(userId, request.Latitude, request.Longitude, request.Address);
            return Ok(ApiResponse<Core.DTOs.UserDto>.Ok(user, "Home location saved successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("home-location")]
    public async Task<IActionResult> GetHomeLocation()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var user = await _authService.GetHomeLocationAsync(userId);
        if (user == null) return NotFound(ApiResponse<object>.Fail("User not found"));
        return Ok(ApiResponse<Core.DTOs.UserDto>.Ok(user));
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
        if (creatorRole != "SH" && creatorRole != "SCA")
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
        if (creatorRole != "SH" && creatorRole != "SCA")
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
    public async Task<IActionResult> DeleteRegion(int id, [FromBody] RegionDeleteRequest? request = null)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH" && creatorRole != "SCA")
            return Forbid();

        var region = await _unitOfWork.Regions.Query()
            .Include(r => r.Zones)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (region == null)
            return NotFound(ApiResponse<object>.Fail("Region not found"));

        var zoneIds = region.Zones.Select(z => z.Id).ToList();

        // All affected users: direct region users + every user in the region's zones
        var affectedUsers = await _unitOfWork.Users.Query()
            .Where(u => (u.RegionId == id && u.ZoneId == null) ||
                        (u.ZoneId != null && zoneIds.Contains(u.ZoneId.Value)))
            .Select(u => new { u.Id, u.Name, u.Email, Role = u.Role.ToString(), u.ZoneId, u.RegionId })
            .ToListAsync();

        var hasUsers = affectedUsers.Any();
        var hasZones = region.Zones.Any();
        var hasContent = hasUsers || hasZones;

        // First call (no body) → return full user + zone list for the transfer modal.
        if (hasContent && request == null)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Region has zones or users. Assign all users to another region before deleting.",
                Data = new
                {
                    users = affectedUsers,
                    zones = region.Zones.Select(z => new { z.Id, z.Name }).ToList()
                }
            });
        }

        if (hasUsers)
        {
            var transfers = request!.UserTransfers ?? new List<UserTransferItem>();
            foreach (var t in transfers)
            {
                if (t.TargetId == id)
                    return BadRequest(ApiResponse<object>.Fail("Target region must differ from the region being deleted."));
                var targetRegion = await _unitOfWork.Regions.GetByIdAsync(t.TargetId);
                if (targetRegion == null)
                    return BadRequest(ApiResponse<object>.Fail($"Target region {t.TargetId} not found."));
                var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Id == t.UserId);
                if (user == null) continue;
                user.RegionId = t.TargetId;
                user.ZoneId = null; // zone is being deleted with the region
                await _unitOfWork.Users.UpdateAsync(user);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        // Delete all zones belonging to this region before deleting the region itself
        foreach (var zone in region.Zones.ToList())
        {
            await _unitOfWork.Zones.DeleteAsync(zone);
        }
        if (hasZones) await _unitOfWork.SaveChangesAsync();

        await _unitOfWork.Regions.DeleteAsync(region);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, hasContent ? "Users transferred · region and zones deleted" : "Region deleted"));
    }

    // ── Zone CRUD ──

    [Authorize]
    [HttpPost("zones")]
    public async Task<IActionResult> CreateZone([FromBody] CreateZoneRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "RH" && creatorRole != "SH" && creatorRole != "SCA")
            return Forbid();

        var name = request.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
            return BadRequest(ApiResponse<object>.Fail("Zone name is required"));

        if (request.RegionId <= 0)
            return BadRequest(ApiResponse<object>.Fail("Region is required"));

        // RH is scoped to their own region
        if (creatorRole == "RH")
        {
            var creatorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var creator = await _unitOfWork.Users.GetByIdAsync(creatorId);
            if (creator?.RegionId == null)
                return BadRequest(ApiResponse<object>.Fail("No region is assigned to your account."));
            if (creator.RegionId != request.RegionId)
                return Forbid();
        }

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
        if (creatorRole != "RH" && creatorRole != "SH" && creatorRole != "SCA")
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

        // RH can only touch zones in their own region (and can't move a zone out of it)
        if (creatorRole == "RH")
        {
            var creatorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var creator = await _unitOfWork.Users.GetByIdAsync(creatorId);
            if (creator?.RegionId == null)
                return BadRequest(ApiResponse<object>.Fail("No region is assigned to your account."));
            if (zone.RegionId != creator.RegionId || request.RegionId != creator.RegionId)
                return Forbid();
        }

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
    public async Task<IActionResult> DeleteZone(int id, [FromBody] ZoneDeleteRequest? request = null)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "RH" && creatorRole != "SH" && creatorRole != "SCA")
            return Forbid();

        var zone = await _unitOfWork.Zones.Query()
            .Include(z => z.Users)
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
            return NotFound(ApiResponse<object>.Fail("Zone not found"));

        if (creatorRole == "RH")
        {
            var creatorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var creator = await _unitOfWork.Users.GetByIdAsync(creatorId);
            if (creator?.RegionId == null || zone.RegionId != creator.RegionId)
                return Forbid();
        }

        var zoneUsers = zone.Users.ToList(); // snapshot before any modification
        var hadUsers = zoneUsers.Any();
        if (hadUsers)
        {
            // First call (no body) → return user list for the transfer modal.
            if (request == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = creatorRole == "RH"
                        ? "Cannot delete zone with assigned users."
                        : "Zone has assigned users. Select where to move each user before deleting.",
                    Data = new
                    {
                        users = zoneUsers.Select(u => new
                        {
                            u.Id, u.Name, u.Email,
                            Role = u.Role.ToString(),
                            u.ZoneId, u.RegionId
                        }).ToList()
                    }
                });
            }

            if (creatorRole != "SCA" && creatorRole != "SH")
                return Forbid();

            var transfers = request.UserTransfers ?? new List<UserTransferItem>();
            foreach (var t in transfers)
            {
                if (t.TargetId == id)
                    return BadRequest(ApiResponse<object>.Fail("Target zone must differ from the zone being deleted."));
                var target = await _unitOfWork.Zones.GetByIdAsync(t.TargetId);
                if (target == null)
                    return BadRequest(ApiResponse<object>.Fail($"Target zone {t.TargetId} not found."));
                var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Id == t.UserId);
                if (user == null) continue;
                user.ZoneId = target.Id;
                user.RegionId = target.RegionId;
                await _unitOfWork.Users.UpdateAsync(user);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        await _unitOfWork.Zones.DeleteAsync(zone);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, hadUsers ? "Zone transferred and deleted" : "Zone deleted"));
    }

    // ── User lists for edit modals ──

    [Authorize]
    [HttpGet("regions/{id}/users")]
    public async Task<IActionResult> GetRegionUsers(int id)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH" && creatorRole != "SCA")
            return Forbid();

        var users = await _unitOfWork.Users.Query()
            .Where(u => u.RegionId == id && u.ZoneId == null)
            .Select(u => new { u.Id, u.Name, u.Email, Role = u.Role.ToString(), u.ZoneId, u.RegionId })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(users));
    }

    [Authorize]
    [HttpGet("zones/{id}/users")]
    public async Task<IActionResult> GetZoneUsers(int id)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH" && creatorRole != "SCA" && creatorRole != "RH")
            return Forbid();

        var users = await _unitOfWork.Users.Query()
            .Where(u => u.ZoneId == id)
            .Select(u => new { u.Id, u.Name, u.Email, Role = u.Role.ToString(), u.ZoneId, u.RegionId })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(users));
    }

    // ── Transfer users without deleting (used by edit flow) ──

    [Authorize]
    [HttpPost("regions/{id}/transfer-users")]
    public async Task<IActionResult> TransferRegionUsers(int id, [FromBody] RegionDeleteRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH" && creatorRole != "SCA")
            return Forbid();

        foreach (var t in request.UserTransfers ?? new List<UserTransferItem>())
        {
            var target = await _unitOfWork.Regions.GetByIdAsync(t.TargetId);
            if (target == null)
                return BadRequest(ApiResponse<object>.Fail($"Target region {t.TargetId} not found."));
            var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Id == t.UserId);
            if (user == null) continue;
            user.RegionId = t.TargetId;
            await _unitOfWork.Users.UpdateAsync(user);
        }

        await _unitOfWork.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Users transferred"));
    }

    [Authorize]
    [HttpPost("zones/{id}/transfer-users")]
    public async Task<IActionResult> TransferZoneUsers(int id, [FromBody] ZoneDeleteRequest request)
    {
        var creatorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (creatorRole != "SH" && creatorRole != "SCA")
            return Forbid();

        foreach (var t in request.UserTransfers ?? new List<UserTransferItem>())
        {
            var target = await _unitOfWork.Zones.GetByIdAsync(t.TargetId);
            if (target == null)
                return BadRequest(ApiResponse<object>.Fail($"Target zone {t.TargetId} not found."));
            var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Id == t.UserId);
            if (user == null) continue;
            user.ZoneId = target.Id;
            user.RegionId = target.RegionId;
            await _unitOfWork.Users.UpdateAsync(user);
        }

        await _unitOfWork.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Users transferred"));
    }
}

public class UserTransferItem
{
    public int UserId { get; set; }
    public int TargetId { get; set; }
}

public class RegionDeleteRequest
{
    public List<UserTransferItem>? UserTransfers { get; set; }
}

public class ZoneDeleteRequest
{
    public List<UserTransferItem>? UserTransfers { get; set; }
}
