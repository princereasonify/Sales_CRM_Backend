using SalesCRM.Core.DTOs;
using SalesCRM.Core.DTOs.Target;

namespace SalesCRM.Core.Interfaces;

public interface ITargetService
{
    Task<TargetAssignmentDto> CreateTargetAsync(CreateTargetRequest request, int assignedById);
    Task<List<TargetAssignmentDto>> GetMyTargetsAsync(int userId);
    Task<List<TargetAssignmentDto>> GetAssignedByMeAsync(int userId);
    Task<List<TargetAssignmentDto>> GetSubTargetsAsync(int parentTargetId);
    Task<TargetAssignmentDto> UpdateProgressAsync(int targetId, UpdateTargetRequest request, int userId);
    Task<TargetAssignmentDto> SubmitTargetAsync(int targetId, int userId);
    Task<TargetAssignmentDto> ReviewTargetAsync(int targetId, ReviewTargetRequest request, int userId);
    Task DeleteTargetAsync(int targetId, int userId);
    Task<List<UserDto>> GetAssignableUsersAsync(int userId, string userRole);
    Task<List<TargetAssignmentDto>> GetFullHierarchyAsync(int targetId);
}
