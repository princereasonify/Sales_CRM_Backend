using SalesCRM.Core.DTOs.Routes;

namespace SalesCRM.Core.Interfaces;

public interface IRoutePlanService
{
    Task<RoutePlanDto?> GetTodayPlanAsync(int userId);
    Task<RoutePlanDto> CreatePlanAsync(CreateRoutePlanRequest request, int userId);
    Task<RoutePlanDto?> UpdatePlanAsync(int id, UpdateRoutePlanRequest request);
}
