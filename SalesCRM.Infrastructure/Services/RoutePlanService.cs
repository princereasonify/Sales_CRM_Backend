using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Routes;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class RoutePlanService : IRoutePlanService
{
    private readonly IUnitOfWork _uow;
    public RoutePlanService(IUnitOfWork uow) => _uow = uow;

    private static RoutePlanDto ToDto(DailyRoutePlan r) => new()
    {
        Id = r.Id, UserId = r.UserId, PlanDate = r.PlanDate, Status = r.Status.ToString(),
        Stops = r.Stops, TotalEstimatedDistanceKm = r.TotalEstimatedDistanceKm,
        TotalEstimatedDurationMinutes = r.TotalEstimatedDurationMinutes,
        TotalActualDistanceKm = r.TotalActualDistanceKm,
        OptimizationMethod = r.OptimizationMethod, CreatedAt = r.CreatedAt
    };

    public async Task<RoutePlanDto?> GetTodayPlanAsync(int userId)
    {
        var today = DateTime.UtcNow.Date;
        var plan = await _uow.DailyRoutePlans.Query()
            .Where(r => r.UserId == userId && r.PlanDate.Date == today)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
        return plan == null ? null : ToDto(plan);
    }

    public async Task<RoutePlanDto> CreatePlanAsync(CreateRoutePlanRequest request, int userId)
    {
        var plan = new DailyRoutePlan
        {
            UserId = userId,
            PlanDate = DateTime.SpecifyKind(request.PlanDate.Date, DateTimeKind.Utc),
            Stops = request.Stops,
            TotalEstimatedDistanceKm = request.TotalEstimatedDistanceKm,
            TotalEstimatedDurationMinutes = request.TotalEstimatedDurationMinutes,
            OptimizationMethod = request.OptimizationMethod,
            Status = RoutePlanStatus.Active
        };
        await _uow.DailyRoutePlans.AddAsync(plan);
        await _uow.SaveChangesAsync();
        return ToDto(plan);
    }

    public async Task<RoutePlanDto?> UpdatePlanAsync(int id, UpdateRoutePlanRequest request)
    {
        var plan = await _uow.DailyRoutePlans.GetByIdAsync(id);
        if (plan == null) return null;
        if (request.Stops != null) plan.Stops = request.Stops;
        if (request.Status != null && Enum.TryParse<RoutePlanStatus>(request.Status, true, out var st)) plan.Status = st;
        if (request.TotalActualDistanceKm.HasValue) plan.TotalActualDistanceKm = request.TotalActualDistanceKm;
        await _uow.SaveChangesAsync();
        return ToDto(plan);
    }
}
