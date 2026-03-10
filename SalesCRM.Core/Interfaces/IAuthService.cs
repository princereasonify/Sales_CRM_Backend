using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Auth;

namespace SalesCRM.Core.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    string GenerateToken(Entities.User user);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, string creatorRole);
    Task<List<UserDto>> GetUsersCreatedByRoleAsync(string creatorRole, int creatorId);
    Task<UserDto> UpdateUserAsync(int userId, UpdateUserRequest request, string creatorRole);
    Task DeleteUserAsync(int userId, string creatorRole);
}
