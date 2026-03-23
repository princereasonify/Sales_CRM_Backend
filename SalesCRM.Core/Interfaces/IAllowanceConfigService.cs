using SalesCRM.Core.DTOs.Allowance;

namespace SalesCRM.Core.Interfaces;

public interface IAllowanceConfigService
{
    Task<List<AllowanceConfigDto>> GetAllConfigsAsync();
    Task<AllowanceConfigDto> CreateConfigAsync(CreateAllowanceConfigRequest request, int setById);
    Task<ResolvedAllowanceDto> ResolveForUserAsync(int userId);
}
