using SalesCRM.Core.DTOs.Allowance;

namespace SalesCRM.Core.Interfaces;

public interface IAllowanceConfigService
{
    Task<List<AllowanceConfigDto>> GetAllConfigsAsync();
    Task<AllowanceConfigDto> CreateConfigAsync(CreateAllowanceConfigRequest request, int setById);
    Task<AllowanceConfigDto?> UpdateConfigAsync(int id, UpdateAllowanceConfigRequest request);
    Task<bool> DeleteConfigAsync(int id);
    Task<ResolvedAllowanceDto> ResolveForUserAsync(int userId);
}
