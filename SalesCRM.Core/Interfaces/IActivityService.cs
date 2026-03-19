using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Common;

namespace SalesCRM.Core.Interfaces;

public interface IActivityService
{
    Task<PaginatedResult<ActivityDto>> GetActivitiesAsync(int foId, PaginationParams pagination, string? type);
    Task<List<ActivityDto>> GetTeamActivitiesAsync(int managerId, string managerRole, int foId);
    Task<ActivityDto> CreateActivityAsync(CreateActivityRequest request, int foId);
    Task UpdatePhotoUrlAsync(int activityId, int userId, string photoUrl);
}
