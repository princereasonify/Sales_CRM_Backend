using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
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
    private readonly INotificationService? _notify;

    public AuthService(IUnitOfWork unitOfWork, IConfiguration configuration, INotificationService? notify = null)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _notify = notify;
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

        if (!user.IsActive)
            throw new InvalidOperationException("Your account is pending approval. Please wait for admin to activate your account.");

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
                Region = user.Region?.Name,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive
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

        ValidatePassword(request.Password);

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
            PhoneNumber = saved.PhoneNumber,
            IsActive = saved.IsActive,
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
            .Where(u => manageableRoles.Contains(u.Role) && u.IsActive);

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
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
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
        {
            ValidatePassword(request.Password);
            user.PasswordHash = HashPassword(request.Password.Trim());
        }

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
            PhoneNumber = saved.PhoneNumber,
            IsActive = saved.IsActive,
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

    public async Task<UserDto> SignupAsync(SignupRequest request)
    {
        var allowedRoles = new[] { "FO", "ZH", "RH", "SH" };
        if (!allowedRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid role. Allowed roles: FO, ZH, RH, SH");

        ValidatePassword(request.Password);

        var targetRole = Enum.Parse<UserRole>(request.Role, true);

        // Check if email already exists
        var existing = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.Trim().ToLowerInvariant());
        if (existing != null)
            throw new InvalidOperationException("A user with this email already exists");

        var fullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}";
        var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var avatar = nameParts.Length >= 2
            ? $"{nameParts[0][0]}{nameParts[^1][0]}".ToUpper()
            : (nameParts.Length == 1 ? nameParts[0][..Math.Min(2, nameParts[0].Length)].ToUpper() : "U");

        var user = new User
        {
            Name = fullName,
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = HashPassword(request.Password.Trim()),
            Role = targetRole,
            Avatar = avatar,
            PhoneNumber = request.PhoneNumber?.Trim(),
            IsActive = false,
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Notify all SCA admins about new signup
        if (_notify != null)
        {
            try
            {
                var scaUsers = await _unitOfWork.Users.Query()
                    .Where(u => u.Role == UserRole.SCA && u.IsActive).ToListAsync();
                foreach (var sca in scaUsers)
                    await _notify.CreateNotificationAsync(sca.Id, NotificationType.Urgent,
                        "New User Signup", $"{user.Name} ({user.Role}) signed up and is pending approval.");
            }
            catch { }
        }

        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            Avatar = user.Avatar,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
        };
    }

    public async Task<UserDto> ApproveUserAsync(int userId)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new InvalidOperationException("User not found");

        if (user.IsActive)
            throw new InvalidOperationException("User is already active");

        user.IsActive = true;
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            Avatar = user.Avatar,
            ZoneId = user.ZoneId,
            Zone = user.Zone?.Name,
            RegionId = user.RegionId,
            Region = user.Region?.Name,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
        };
    }

    public async Task RejectUserAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        if (user.IsActive)
            throw new InvalidOperationException("Cannot reject an active user");

        await _unitOfWork.Users.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<List<UserDto>> GetPendingUsersAsync()
    {
        var users = await _unitOfWork.Users.Query()
            .Where(u => !u.IsActive)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return users.Select(u => new UserDto
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Role = u.Role.ToString(),
            Avatar = u.Avatar,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive,
        }).ToList();
    }

    public async Task<UserDto> SetHomeLocationAsync(int userId, decimal latitude, decimal longitude, string? address = null)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new InvalidOperationException("User not found");

        user.HomeLatitude = latitude;
        user.HomeLongitude = longitude;
        user.HomeAddress = address;
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id, Name = user.Name, Email = user.Email,
            Role = user.Role.ToString(), Avatar = user.Avatar,
            ZoneId = user.ZoneId, Zone = user.Zone?.Name,
            RegionId = user.RegionId, Region = user.Region?.Name,
            PhoneNumber = user.PhoneNumber, IsActive = user.IsActive,
            HomeLatitude = user.HomeLatitude, HomeLongitude = user.HomeLongitude, HomeAddress = user.HomeAddress,
        };
    }

    public async Task<UserDto?> GetHomeLocationAsync(int userId)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id, Name = user.Name, Email = user.Email,
            Role = user.Role.ToString(), Avatar = user.Avatar,
            HomeLatitude = user.HomeLatitude, HomeLongitude = user.HomeLongitude, HomeAddress = user.HomeAddress,
        };
    }

    public async Task RequestAccountDeletionAsync(string email, string password)
    {
        var emailLower = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var pwd = password?.Trim() ?? string.Empty;

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);

        if (user == null || !VerifyPassword(pwd, user.PasswordHash))
            throw new InvalidOperationException("Invalid email or password");

        // Send deletion confirmation email
        var smtpHost = _configuration["Smtp:Host"];
        var smtpPort = int.TryParse(_configuration["Smtp:Port"], out var port) ? port : 587;
        var smtpUser = _configuration["Smtp:Username"];
        var smtpPass = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"];
        var fromName = _configuration["Smtp:FromName"] ?? "SalesCRM";

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            throw new InvalidOperationException("Email service is not configured. Please contact admin.");

        using var msg = new MailMessage();
        msg.From = new MailAddress(fromEmail!, fromName);
        msg.To.Add(user.Email);
        msg.Subject = "Account Deletion Request - EduCRM";
        msg.IsBodyHtml = true;
        msg.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: linear-gradient(135deg, #0d9488, #0f766e); padding: 30px; text-align: center; border-radius: 12px 12px 0 0;'>
                    <h1 style='color: white; margin: 0; font-size: 24px;'>EduCRM</h1>
                </div>
                <div style='padding: 30px; background: #ffffff; border: 1px solid #e5e7eb; border-top: none; border-radius: 0 0 12px 12px;'>
                    <h2 style='color: #1f2937; margin-top: 0;'>Account Deletion Request Received</h2>
                    <p style='color: #4b5563; line-height: 1.6;'>Hi <strong>{user.Name}</strong>,</p>
                    <p style='color: #4b5563; line-height: 1.6;'>We have received your request to delete your EduCRM account associated with <strong>{user.Email}</strong>.</p>
                    <div style='background: #fef3c7; border: 1px solid #fcd34d; border-radius: 8px; padding: 16px; margin: 20px 0;'>
                        <p style='color: #92400e; margin: 0; font-weight: 600;'>Your account will be deleted within 7-15 business days.</p>
                    </div>
                    <p style='color: #4b5563; line-height: 1.6;'>If you did not make this request, please contact our support team immediately.</p>
                    <p style='color: #9ca3af; font-size: 13px; margin-top: 30px;'>Thank you,<br/>The EduCRM Team</p>
                </div>
            </div>";

        using var smtp = new SmtpClient(smtpHost, smtpPort);
        smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);
        smtp.EnableSsl = true;
        await smtp.SendMailAsync(msg);
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new InvalidOperationException("Password must be at least 8 characters long.");
        if (!password.Any(char.IsUpper))
            throw new InvalidOperationException("Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsDigit))
            throw new InvalidOperationException("Password must contain at least one digit.");
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            throw new InvalidOperationException("Password must contain at least one special character.");
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
