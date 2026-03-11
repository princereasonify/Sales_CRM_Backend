using SalesCRM.Core.DTOs;

namespace SalesCRM.Core.Interfaces;

public interface IDashboardService
{
    Task<FoDashboardDto> GetFoDashboardAsync(int foId);
    Task<ZoneDashboardDto> GetZoneDashboardAsync(int zhId);
    Task<RegionDashboardDto> GetRegionDashboardAsync(int rhId);
    Task<NationalDashboardDto> GetNationalDashboardAsync();
    Task<List<FoPerformanceDto>> GetTeamPerformanceAsync(int zhId);
    Task<List<UserPerformanceDto>> GetPerformanceTrackingAsync(int userId, string userRole);
}
