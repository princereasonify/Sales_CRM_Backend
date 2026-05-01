using SalesCRM.Core.DTOs.Reports;

namespace SalesCRM.Core.Interfaces;

public interface IReportService
{
    Task<List<UserPerformanceDto>> GetUserPerformanceAsync(ReportFilters filters);
    Task<List<SchoolVisitSummaryDto>> GetSchoolVisitSummaryAsync(ReportFilters filters);
    Task<List<PipelineReportDto>> GetPipelineReportAsync(ReportFilters filters);
    Task<LostDealAnalysisDto> GetLostDealAnalysisAsync(ReportFilters filters);
}
