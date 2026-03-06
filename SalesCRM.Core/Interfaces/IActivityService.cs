using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;

namespace SalesCRM.Core.Interfaces;

public interface IActivityService
{
    Task<PaginatedResult<ActivityDto>> GetActivitiesAsync(int foId, PaginationParams pagination, string? type);
    Task<ActivityDto> CreateActivityAsync(CreateActivityRequest request, int foId);
}
