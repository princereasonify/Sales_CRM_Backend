using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;

namespace SalesCRM.Core.Interfaces;

public interface IDealService
{
    Task<PaginatedResult<DealDto>> GetDealsAsync(int userId, PaginationParams pagination);
    Task<DealDto?> GetDealByIdAsync(int id, int userId);
    Task<DealDto> CreateDealAsync(CreateDealRequest request, int foId);
    Task<DealDto?> ApproveDealAsync(int dealId, DealApprovalRequest request, int approverId);
    Task<List<DealDto>> GetPendingApprovalsAsync(int zhId);
}
