using SalesCRM.Core.DTOs.WeeklyPlan;

namespace SalesCRM.Core.Interfaces;

public interface IWeeklyPlanService
{
    Task<WeeklyPlanDto?> GetMyPlanAsync(int userId, DateTime weekStart);
    Task<List<WeeklyPlanDto>> GetTeamPlansAsync(int managerId, string role, DateTime weekStart);
    Task<WeeklyPlanDto> CreatePlanAsync(CreateWeeklyPlanRequest request, int userId);
    Task<WeeklyPlanDto?> UpdatePlanAsync(int id, UpdateWeeklyPlanRequest request, int userId);
    Task<WeeklyPlanDto?> SubmitPlanAsync(int id, int userId);
    Task<WeeklyPlanDto?> ApprovePlanAsync(int id, int reviewerId);
    Task<WeeklyPlanDto?> EditPlanAsync(int id, ManagerEditRequest request, int reviewerId);
    Task<WeeklyPlanDto?> RejectPlanAsync(int id, RejectPlanRequest request, int reviewerId);
}
