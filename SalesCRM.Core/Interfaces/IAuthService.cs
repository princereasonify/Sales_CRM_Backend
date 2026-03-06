using SalesCRM.Core.DTOs.Auth;

namespace SalesCRM.Core.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    string GenerateToken(Entities.User user);
}
