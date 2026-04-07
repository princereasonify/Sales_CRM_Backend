using SalesCRM.Core.DTOs;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;

namespace SalesCRM.Core.Interfaces;

public interface IAiReportService
{
    // Data collection
    Task<string> CollectFoDailyDataAsync(int foId, DateTime date);
    Task<string> CollectManagementDataAsync(int managerId, DateTime periodStart, DateTime periodEnd);

    // Single report generation
    Task<AiReport> GenerateFoDailyReportAsync(int foId, DateTime date);
    Task<AiReport> GenerateFoPeriodReportAsync(int foId, AiReportType reportType, DateTime periodStart, DateTime periodEnd);
    Task<AiReport> GenerateManagementReportAsync(int managerId, AiReportType reportType, DateTime periodStart, DateTime periodEnd);

    // Batch operations
    Task GenerateAllFoDailyReportsAsync(DateTime date);
    Task GenerateAllFoWeeklyReportsAsync(DateTime weekStart, DateTime weekEnd);
    Task GenerateAllFoMonthlyReportsAsync(DateTime monthStart, DateTime monthEnd);
    Task GenerateAllManagementReportsAsync(DateTime periodStart, DateTime periodEnd);

    // Retrieval
    Task<AiReportDetailDto?> GetReportAsync(int reportId, int requestingUserId, string requestingUserRole);
    Task<List<AiReportListDto>> GetReportsAsync(int requestingUserId, string requestingUserRole, AiReportFilterDto filters);
}
