using SalesCRM.Core.DTOs;

namespace SalesCRM.Core.Interfaces;

public interface IDashboardService
{
    Task<FoDashboardDto> GetFoDashboardAsync(int foId, string period = "today");
    Task<ZoneDashboardDto> GetZoneDashboardAsync(int zhId, string period = "month");
    Task<RegionDashboardDto> GetRegionDashboardAsync(int rhId, string period = "month");
    Task<NationalDashboardDto> GetNationalDashboardAsync(string period = "month");
    Task<ScaDashboardDto> GetScaDashboardAsync(string period = "month");
    Task<List<FoPerformanceDto>> GetTeamPerformanceAsync(int userId, string userRole);
    Task<List<UserPerformanceDto>> GetPerformanceTrackingAsync(int userId, string userRole);
    Task<List<ReportableUserDto>> GetReportableUsersAsync(int userId, string userRole);
}
