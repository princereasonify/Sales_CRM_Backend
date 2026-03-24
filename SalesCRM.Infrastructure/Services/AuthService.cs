using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Auth;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AuthService(IUnitOfWork unitOfWork, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var password = request.Password?.Trim() ?? string.Empty;

        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user == null || !VerifyPassword(password, user.PasswordHash))
            return null;

        return new LoginResponse
        {
            Token = GenerateToken(user),
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.ToString(),
                Avatar = user.Avatar,
                ZoneId = user.ZoneId,
                Zone = user.Zone?.Name,
                RegionId = user.RegionId,
                Region = user.Region?.Name
            }
        };
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool IsRoleAllowed(string creatorRole, UserRole targetRole)
    {
        return creatorRole switch
        {
            "SCA" => targetRole is UserRole.SH or UserRole.RH or UserRole.ZH or UserRole.FO,
            "SH" => targetRole is UserRole.RH or UserRole.ZH or UserRole.FO,
            "RH" => targetRole is UserRole.ZH or UserRole.FO,
            "ZH" => targetRole == UserRole.FO,
            _ => false
        };
    }

    private static List<UserRole> GetManageableRoles(string creatorRole)
    {
        return creatorRole switch
        {
            "SCA" => new List<UserRole> { UserRole.SH, UserRole.RH, UserRole.ZH, UserRole.FO },
            "SH" => new List<UserRole> { UserRole.RH, UserRole.ZH, UserRole.FO },
            "RH" => new List<UserRole> { UserRole.ZH, UserRole.FO },
            "ZH" => new List<UserRole> { UserRole.FO },
            _ => new List<UserRole>()
        };
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, string creatorRole)
    {
        var targetRole = Enum.Parse<UserRole>(request.Role, true);
        var allowed = IsRoleAllowed(creatorRole, targetRole);

        if (!allowed)
            throw new InvalidOperationException($"{creatorRole} cannot create users with role {request.Role}");

        // Check if email already exists
        var existing = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.Trim().ToLowerInvariant());
        if (existing != null)
            throw new InvalidOperationException("A user with this email already exists");

        // Generate avatar from name initials
        var nameParts = request.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var avatar = nameParts.Length >= 2
            ? $"{nameParts[0][0]}{nameParts[^1][0]}".ToUpper()
            : (nameParts.Length == 1 ? nameParts[0][..Math.Min(2, nameParts[0].Length)].ToUpper() : "U");

        // Auto-derive regionId from zone if not provided
        int? regionId = request.RegionId;
        if (regionId == null && request.ZoneId != null)
        {
            var zone = await _unitOfWork.Zones.GetByIdAsync(request.ZoneId.Value);
            regionId = zone?.RegionId;
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = HashPassword(request.Password.Trim()),
            Role = targetRole,
            Avatar = avatar,
            ZoneId = request.ZoneId,
            RegionId = regionId,
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Reload with zone/region
        var saved = await _unitOfWork.Users.Query()
            .Include(u => u.Zone).ThenInclude(z => z.Region)
            .Include(u => u.Region)
            .FirstAsync(u => u.Id == user.Id);

        return new UserDto
        {
            Id = saved.Id,
            Name = saved.Name,
            Email = saved.Email,
            Role = saved.Role.ToString(),
            Avatar = saved.Avatar,
            ZoneId = saved.ZoneId,
            Zone = saved.Zone?.Name,
            RegionId = saved.RegionId ?? saved.Zone?.RegionId,
            Region = saved.Region?.Name ?? saved.Zone?.Region?.Name,
        };
    }

    public async Task<List<UserDto>> GetUsersCreatedByRoleAsync(string creatorRole, int creatorId)
    {
        var manageableRoles = GetManageableRoles(creatorRole);
        if (manageableRoles.Count == 0)
            return new List<UserDto>();

        var query = _unitOfWork.Users.Query()
            .Include(u => u.Zone).ThenInclude(z => z.Region)
            .Include(u => u.Region)
            .Where(u => manageableRoles.Contains(u.Role));

        // ZH can only see FOs in their zone
        if (creatorRole == "ZH")
        {
            var creator = await _unitOfWork.Users.GetByIdAsync(creatorId);
            if (creator?.ZoneId != null)
                query = query.Where(u => u.ZoneId == creator.ZoneId);
        }

        // RH scoping: see ZHs/FOs in their region
        if (creatorRole == "RH")
        {
            var creator = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.Id == creatorId);
            if (creator?.RegionId != null)
                query = query.Where(u => u.RegionId == creator.RegionId);
        }

        // SH and SCA see all — no scoping needed

        var users = await query.OrderBy(u => u.Role).ThenBy(u => u.Name).ToListAsync();

        // Build lookup maps for parent head names
        var allHeads = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.RH || u.Role == UserRole.ZH)
            .ToListAsync();

        var rhByRegion = allHeads
            .Where(u => u.Role == UserRole.RH && u.RegionId != null)
            .GroupBy(u => u.RegionId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var zhByZone = allHeads
            .Where(u => u.Role == UserRole.ZH && u.ZoneId != null)
            .GroupBy(u => u.ZoneId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Name);

        return users.Select(u =>
        {
            // Derive region from zone if user has no direct regionId
            var regionId = u.RegionId ?? u.Zone?.RegionId;
            var regionName = u.Region?.Name ?? u.Zone?.Region?.Name;

            return new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                Avatar = u.Avatar,
                ZoneId = u.ZoneId,
                Zone = u.Zone?.Name,
                RegionId = regionId,
                Region = regionName,
                RegionalHead = regionId != null && rhByRegion.TryGetValue(regionId.Value, out var rhName) ? rhName : null,
                ZonalHead = u.ZoneId != null && zhByZone.TryGetValue(u.ZoneId.Value, out var zhName) ? zhName : null,
            };
        }).ToList();
    }

    public async Task<UserDto> UpdateUserAsync(int userId, UpdateUserRequest request, string creatorRole)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new InvalidOperationException("User not found");

        // Verify current role is manageable
        if (!IsRoleAllowed(creatorRole, user.Role))
            throw new InvalidOperationException($"{creatorRole} cannot manage users with role {user.Role}");

        // If role is being changed, verify new role is also allowed
        var newRole = Enum.Parse<UserRole>(request.Role, true);
        if (!IsRoleAllowed(creatorRole, newRole))
            throw new InvalidOperationException($"{creatorRole} cannot assign role {request.Role}");

        // Check email uniqueness (exclude current user)
        var emailLower = request.Email.Trim().ToLowerInvariant();
        var emailExists = await _unitOfWork.Users.Query()
            .AnyAsync(u => u.Email.ToLower() == emailLower && u.Id != userId);
        if (emailExists)
            throw new InvalidOperationException("A user with this email already exists");

        // Auto-derive regionId from zone if not provided
        int? regionId = request.RegionId;
        if (regionId == null && request.ZoneId != null)
        {
            var zone = await _unitOfWork.Zones.GetByIdAsync(request.ZoneId.Value);
            regionId = zone?.RegionId;
        }

        // Update fields
        user.Name = request.Name.Trim();
        user.Email = emailLower;
        user.Role = newRole;
        user.ZoneId = request.ZoneId;
        user.RegionId = regionId;

        // Update avatar from new name
        var nameParts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        user.Avatar = nameParts.Length >= 2
            ? $"{nameParts[0][0]}{nameParts[^1][0]}".ToUpper()
            : (nameParts.Length == 1 ? nameParts[0][..Math.Min(2, nameParts[0].Length)].ToUpper() : "U");

        // Update password only if provided
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = HashPassword(request.Password.Trim());

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Reload
        var saved = await _unitOfWork.Users.Query()
            .Include(u => u.Zone).ThenInclude(z => z.Region)
            .Include(u => u.Region)
            .FirstAsync(u => u.Id == user.Id);

        return new UserDto
        {
            Id = saved.Id,
            Name = saved.Name,
            Email = saved.Email,
            Role = saved.Role.ToString(),
            Avatar = saved.Avatar,
            ZoneId = saved.ZoneId,
            Zone = saved.Zone?.Name,
            RegionId = saved.RegionId ?? saved.Zone?.RegionId,
            Region = saved.Region?.Name ?? saved.Zone?.Region?.Name,
        };
    }

    public async Task DeleteUserAsync(int userId, string creatorRole)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        if (!IsRoleAllowed(creatorRole, user.Role))
            throw new InvalidOperationException($"{creatorRole} cannot delete users with role {user.Role}");

        await _unitOfWork.Users.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public static string HashPassword(string password)
    {
        using var hmac = new HMACSHA256();
        var salt = hmac.Key;
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var hash = Convert.FromBase64String(parts[1]);

        using var hmac = new HMACSHA256(salt);
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(computedHash, hash);
    }
}
